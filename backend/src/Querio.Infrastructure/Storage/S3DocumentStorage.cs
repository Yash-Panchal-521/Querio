using System.Globalization;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Querio.Application.Common.Abstractions;

namespace Querio.Infrastructure.Storage;

/// <summary>
/// Object storage over the S3 API.
///
/// Named for the protocol rather than for Cloudflare, because that is what it actually depends
/// on: R2 in production and MinIO in tests are the same client and the same code path. Testing
/// against a real MinIO container therefore proves the storage logic rather than a mock of it.
/// </summary>
internal sealed class S3DocumentStorage : IDocumentStorage
{
    private readonly IAmazonS3 client;
    private readonly ObjectStorageOptions options;

    public S3DocumentStorage(IAmazonS3 client, IOptions<ObjectStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.client = client;
        this.options = options.Value;
    }

    /// <summary>
    /// Content-addressed and scoped to the organization that uploaded it. Two consequences,
    /// both wanted: re-uploading identical bytes lands on the same key instead of accumulating
    /// copies, and everything belonging to one organization sits under a prefix that can be
    /// listed or removed as a unit.
    /// </summary>
    private static string BuildKey(Guid tenantId, string contentHash) =>
        string.Create(CultureInfo.InvariantCulture, $"tenants/{tenantId:D}/documents/{contentHash}");

    public async Task<string> StoreAsync(
        Guid tenantId,
        string contentHash,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var key = BuildKey(tenantId, contentHash);

        await client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = options.BucketName,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                // The uploader's file name is metadata, not identity. The key is the hash.
                AutoCloseStream = false,
            },
            cancellationToken);

        return key;
    }

    public async Task<Stream> OpenAsync(string storageKey, CancellationToken cancellationToken)
    {
        var response = await client.GetObjectAsync(
            new GetObjectRequest { BucketName = options.BucketName, Key = storageKey },
            cancellationToken);

        return response.ResponseStream;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        // S3 delete is already idempotent — removing something absent succeeds — which is what
        // lets a half-finished delete be retried without special-casing.
        await client.DeleteObjectAsync(
            new DeleteObjectRequest { BucketName = options.BucketName, Key = storageKey },
            cancellationToken);
    }

    public async Task<Uri> CreateDownloadLinkAsync(
        string storageKey,
        string downloadFileName,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var url = await client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = options.BucketName,
            Key = storageKey,
            Verb = HttpVerb.GET,

            // Set explicitly. A presigned URL is constructed rather than derived from a live
            // request, and the SDK builds it as HTTPS regardless of the endpoint's scheme or
            // the client's UseHttp setting — which is correct for R2 and wrong for a local
            // container, and only fails when someone follows the link.
            Protocol = this.options.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                ? Protocol.HTTP
                : Protocol.HTTPS,
            Expires = DateTime.UtcNow.Add(lifetime),
            // Without this the browser would save the file under its content hash, which is
            // accurate and useless. The quotes matter for names containing spaces.
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentDisposition = string.Create(
                    CultureInfo.InvariantCulture,
                    $"attachment; filename=\"{SanitiseFileName(downloadFileName)}\""),
            },
        });

        return new Uri(url);
    }

    /// <summary>
    /// A quote or newline in the name would break out of the header, so anything that could
    /// is replaced rather than escaped. Names come from whatever the uploader called the file.
    /// </summary>
    private static string SanitiseFileName(string fileName)
    {
        var cleaned = new char[fileName.Length];

        for (var index = 0; index < fileName.Length; index++)
        {
            var character = fileName[index];
            cleaned[index] = char.IsControl(character) || character is '"' or '\\' ? '_' : character;
        }

        return new string(cleaned);
    }
}
