using Amazon.Runtime;
using Amazon.S3;

namespace Querio.Infrastructure.Storage;

/// <summary>
/// Builds the S3 client from options, in one place.
///
/// One place on purpose: tests that configure their own client are testing a configuration
/// nothing ships, and the difference is not always visible. This exists because it happened —
/// a test client that omitted the transport setting below still stored and read objects
/// perfectly, and only failed on presigned links, which is the one path where the client has
/// to construct a URL rather than follow the one it was given.
/// </summary>
public static class ObjectStorageClientFactory
{
    public static IAmazonS3 Create(ObjectStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var config = new AmazonS3Config
        {
            ServiceURL = options.ServiceUrl,

            // R2 and MinIO both address buckets by path. Virtual-host style would put the
            // bucket name in the hostname, which neither serves.
            ForcePathStyle = true,

            // The SDK signs with a region and will not guess one. R2 wants the literal "auto";
            // Backblaze B2 wants its real region and refuses "auto". Configured rather than
            // fixed, because getting it wrong fails at signature verification with a message
            // about credentials rather than about a region.
            AuthenticationRegion = options.Region,

            // Presigned URLs are generated rather than derived from a live request, so the
            // scheme comes from here. Left unset the SDK assumes HTTPS, which is right for R2
            // and wrong for a local container — and only shows up when a link is followed.
            UseHttp = options.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
        };

        return new AmazonS3Client(
            new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey),
            config);
    }
}
