using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Querio.Infrastructure.Storage;
using Testcontainers.Minio;

namespace Querio.Api.Tests.Storage;

/// <summary>
/// Runs the real storage implementation against a real S3 server.
///
/// MinIO rather than a mock, and rather than Cloudflare: the code under test is the same
/// client and the same code path R2 gets, so this proves the storage logic instead of proving
/// that a stub returns what it was told to. It also means continuous integration needs no
/// Cloudflare credentials.
/// </summary>
public sealed class S3DocumentStorageTests : IAsyncLifetime
{
    private const string BucketName = "querio-documents";

    /// <summary>Pinned, so a MinIO release cannot change what these tests run against.</summary>
    private readonly MinioContainer container =
        new MinioBuilder("minio/minio:RELEASE.2025-09-07T16-13-09Z").Build();

    private S3DocumentStorage storage = null!;
    private IAmazonS3 client = null!;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);

        var options = new ObjectStorageOptions
        {
            // Built explicitly rather than from GetConnectionString(): the scheme decides
            // whether presigned links come out as http or https, and a scheme-less value
            // leaves the SDK to assume TLS the container is not serving.
            ServiceUrl = $"http://{container.Hostname}:{container.GetMappedPublicPort(9000)}",
            AccessKeyId = container.GetAccessKey(),
            SecretAccessKey = container.GetSecretKey(),
            BucketName = BucketName,
        };

        // The same factory production uses. Configuring a client here by hand is how the
        // presigned-link failure this test suite caught got in.
        client = ObjectStorageClientFactory.Create(options);

        await client.PutBucketAsync(
            new PutBucketRequest { BucketName = BucketName },
            TestContext.Current.CancellationToken);

        storage = new S3DocumentStorage(client, Options.Create(options));
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();

        await container.DisposeAsync();
    }

    [Fact]
    public async Task Content_round_trips_and_the_key_is_derived_from_the_hash()
    {
        var tenantId = Guid.CreateVersion7();
        const string hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        var key = await StoreAsync(tenantId, hash, "Parental leave is 26 weeks.");

        // The layout is the contract that makes dedupe and per-organization cleanup possible,
        // so it is asserted rather than left implicit.
        key.ShouldBe($"tenants/{tenantId:D}/documents/{hash}");

        await using var stored = await storage.OpenAsync(key, TestContext.Current.CancellationToken);
        using var reader = new StreamReader(stored, Encoding.UTF8);

        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken))
            .ShouldBe("Parental leave is 26 weeks.");
    }

    [Fact]
    public async Task Storing_the_same_content_twice_does_not_accumulate_objects()
    {
        var tenantId = Guid.CreateVersion7();
        const string hash = "1111111111111111111111111111111111111111111111111111111111111111";

        var first = await StoreAsync(tenantId, hash, "Identical bytes.");
        var second = await StoreAsync(tenantId, hash, "Identical bytes.");

        first.ShouldBe(second);

        // Content-addressed keys are what make a retried upload harmless. If this ever grew to
        // two, every re-upload would quietly consume the storage allowance twice.
        var listed = await client.ListObjectsV2Async(
            new ListObjectsV2Request { BucketName = BucketName, Prefix = $"tenants/{tenantId:D}/" },
            TestContext.Current.CancellationToken);

        listed.S3Objects.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Deleting_something_already_gone_is_not_an_error()
    {
        var tenantId = Guid.CreateVersion7();
        const string hash = "2222222222222222222222222222222222222222222222222222222222222222";

        var key = await StoreAsync(tenantId, hash, "Temporary.");

        await storage.DeleteAsync(key, TestContext.Current.CancellationToken);

        // A delete that failed halfway must be safe to retry, so the second call has to be a
        // no-op rather than a failure that strands the document half-removed.
        await Should.NotThrowAsync(
            async () => await storage.DeleteAsync(key, TestContext.Current.CancellationToken));

        await Should.ThrowAsync<AmazonS3Exception>(
            async () => await storage.OpenAsync(key, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_download_link_works_without_credentials_and_names_the_file()
    {
        var tenantId = Guid.CreateVersion7();
        const string hash = "3333333333333333333333333333333333333333333333333333333333333333";

        var key = await StoreAsync(tenantId, hash, "Signed content.");

        var link = await storage.CreateDownloadLinkAsync(
            key,
            "quarterly report.pdf",
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        link.Scheme.ShouldBe("http", $"generated link was: {link}");

        using var anonymous = new HttpClient();
        using var response = await anonymous.GetAsync(link, TestContext.Current.CancellationToken);

        // Anonymous on purpose: the whole point of a presigned link is that the bucket stays
        // private and the browser still fetches directly, without the file passing through us.
        response.EnsureSuccessStatusCode();

        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldBe("Signed content.");

        var disposition = response.Content.Headers.ContentDisposition;
        disposition.ShouldNotBeNull();
        disposition.FileName.ShouldNotBeNull();
        disposition.FileName.ShouldContain("quarterly report.pdf");
    }

    [Fact]
    public async Task A_file_name_cannot_break_out_of_the_download_header()
    {
        var tenantId = Guid.CreateVersion7();
        const string hash = "4444444444444444444444444444444444444444444444444444444444444444";

        var key = await StoreAsync(tenantId, hash, "Quoted.");

        // The name is whatever the uploader called the file. A quote would close the header
        // value early and let the rest be read as further header content.
        var link = await storage.CreateDownloadLinkAsync(
            key,
            "evil\".pdf",
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        using var anonymous = new HttpClient();
        using var response = await anonymous.GetAsync(link, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        var disposition = response.Content.Headers.ContentDisposition;
        disposition.ShouldNotBeNull();
        disposition.FileName.ShouldNotBeNull();
        disposition.FileName.ShouldNotContain("evil\"");
    }

    private async Task<string> StoreAsync(Guid tenantId, string hash, string content)
    {
        using var payload = new MemoryStream(Encoding.UTF8.GetBytes(content));

        return await storage.StoreAsync(
            tenantId,
            hash,
            payload,
            "text/plain",
            TestContext.Current.CancellationToken);
    }
}
