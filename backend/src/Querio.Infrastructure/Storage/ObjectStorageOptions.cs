namespace Querio.Infrastructure.Storage;

/// <summary>
/// Connection details for the S3-compatible bucket that holds uploaded files.
///
/// In production that is Cloudflare R2, whose free allowance is 10 GB with no egress charge —
/// the reason originals live there rather than in Postgres, where the free plan caps the whole
/// database at half a gigabyte. In tests it is MinIO, which speaks the same API, so continuous
/// integration exercises the real storage path without needing Cloudflare credentials.
///
/// Required values are checked while the host is being built; see <c>AddObjectStorage</c>.
/// </summary>
public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    /// <summary>
    /// R2's endpoint is <c>https://{account-id}.r2.cloudflarestorage.com</c>. Local
    /// development and tests point this at a container.
    /// </summary>
    public string ServiceUrl { get; set; } = string.Empty;

    public string AccessKeyId { get; set; } = string.Empty;

    public string SecretAccessKey { get; set; } = string.Empty;

    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// The region the request is signed for. Not where the bytes live — SigV4 requires a region
    /// string and providers disagree on what it should be.
    ///
    /// R2 is not regional and expects the literal <c>auto</c>; MinIO does not care. Backblaze B2
    /// signs with its real region, taken from the endpoint host — <c>us-west-004</c> and the
    /// like — and rejects <c>auto</c> outright. The default keeps R2 and MinIO working
    /// unchanged, so this only needs setting where a provider insists.
    /// </summary>
    public string Region { get; set; } = "auto";

}
