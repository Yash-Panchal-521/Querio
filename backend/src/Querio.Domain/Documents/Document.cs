using Querio.Domain.Common;

namespace Querio.Domain.Documents;

/// <summary>
/// One uploaded file belonging to one organization.
///
/// The row carries progress as well as identity, because "is my document ready" is the
/// question the interface asks constantly and answering it from a counter is far cheaper than
/// counting chunks on every poll.
///
/// The original bytes are not here. Postgres is the expensive, size-limited store; object
/// storage is the cheap one. What lives here is the hash and the key needed to find them.
/// </summary>
public sealed class Document : Entity, IAuditable, IHasTenant
{
    public const int MaxFileNameLength = 260;

    /// <summary>Hex-encoded SHA-256, so exactly 64 characters.</summary>
    public const int ContentHashLength = 64;

    public const int MaxStorageKeyLength = 512;

    public const int MaxFailureReasonLength = 500;

    public const int MaxFailureCodeLength = 64;

    public const int MaxPauseReasonLength = 500;

    private Document()
    {
        FileName = string.Empty;
        ContentHash = string.Empty;
        StorageKey = string.Empty;
    }

    private Document(
        Guid tenantId,
        Guid uploadedByUserId,
        string fileName,
        FileFormat format,
        long byteSize,
        string contentHash,
        string storageKey)
    {
        TenantId = tenantId;
        UploadedByUserId = uploadedByUserId;
        FileName = fileName;
        Format = format;
        ByteSize = byteSize;
        ContentHash = contentHash;
        StorageKey = storageKey;
        Status = DocumentStatus.Pending;
    }

    public Guid TenantId { get; private set; }

    public Guid UploadedByUserId { get; private set; }

    /// <summary>As the uploader named it. Shown back to them, never used to locate the bytes.</summary>
    public string FileName { get; private set; }

    public FileFormat Format { get; private set; }

    public long ByteSize { get; private set; }

    /// <summary>
    /// SHA-256 of the original bytes. Uniquely indexed per organization, so re-uploading the
    /// same file is recognised rather than embedded a second time — which matters because
    /// embedding is the metered resource, not storage.
    /// </summary>
    public string ContentHash { get; private set; }

    public string StorageKey { get; private set; }

    public DocumentStatus Status { get; private set; }

    public int ChunkCount { get; private set; }

    public int EmbeddedChunkCount { get; private set; }

    /// <summary>Stable identifier for the failure, safe to branch on in the interface.</summary>
    public string? FailureCode { get; private set; }

    /// <summary>Plain language, shown to the uploader. Never an exception message.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>
    /// Why the document is paused, in the same plain language as a failure reason. Held on the
    /// document rather than only on the job because the interface reads documents, and the two
    /// pauses need different sentences: a throttle clears in a minute, a spent daily allowance
    /// does not clear until it resets.
    /// </summary>
    public string? PauseReason { get; private set; }

    /// <summary>
    /// When the queue will pick this up again. Also what tells the interface whether to keep
    /// watching: minutes away is worth waiting for, hours away is not.
    /// </summary>
    public DateTimeOffset? ResumesAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static Document Record(
        Guid tenantId,
        Guid uploadedByUserId,
        string fileName,
        FileFormat format,
        long byteSize,
        string contentHash,
        string storageKey) =>
        new(tenantId, uploadedByUserId, fileName, format, byteSize, contentHash, storageKey);

    public void BeginExtracting() => MoveTo(DocumentStatus.Extracting);

    public void BeginChunking() => MoveTo(DocumentStatus.Chunking);

    /// <summary>
    /// Enters the embedding phase, from the start or from wherever a previous run got to.
    ///
    /// <paramref name="alreadyEmbedded"/> is not cosmetic. Embedding is the metered resource,
    /// and a document longer than one minute's token allowance is paused part-way through by
    /// design; resetting the counter would mean re-spending the allowance on passages that are
    /// already vectors, which on a free tier is the difference between finishing and never
    /// finishing.
    /// </summary>
    public void BeginEmbedding(int chunkCount, int alreadyEmbedded = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(chunkCount);
        ArgumentOutOfRangeException.ThrowIfNegative(alreadyEmbedded);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(alreadyEmbedded, chunkCount);

        ChunkCount = chunkCount;
        EmbeddedChunkCount = alreadyEmbedded;
        MoveTo(DocumentStatus.Embedding);
    }

    public void RecordEmbedded(int embeddedChunkCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(embeddedChunkCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(embeddedChunkCount, ChunkCount);

        EmbeddedChunkCount = embeddedChunkCount;
    }

    /// <summary>
    /// Paused rather than broken: the queue resumes on its own, and progress so far is kept.
    /// </summary>
    public void WaitForQuota(string reason, DateTimeOffset resumesAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        MoveTo(DocumentStatus.WaitingForQuota);

        // After MoveTo, which clears them for every other status.
        PauseReason = reason;
        ResumesAt = resumesAt;
    }

    public void MarkReady()
    {
        if (EmbeddedChunkCount != ChunkCount)
        {
            // Ready is what retrieval trusts. Letting it be set while passages are still
            // unembedded would make a document silently answer from half its content.
            throw new InvalidOperationException(
                $"Cannot mark document {Id} ready: {EmbeddedChunkCount} of {ChunkCount} chunks are embedded.");
        }

        MoveTo(DocumentStatus.Ready);
    }

    public void Fail(string failureCode, string failureReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);

        FailureCode = failureCode;
        FailureReason = failureReason;
        MoveTo(DocumentStatus.Failed);
    }

    /// <summary>
    /// Puts the document back to the start so a retry re-derives everything. Progress counters
    /// reset with it, because a retry that kept the old counts would report a document as more
    /// finished than it is.
    /// </summary>
    public void ResetForRetry()
    {
        ChunkCount = 0;
        EmbeddedChunkCount = 0;
        FailureCode = null;
        FailureReason = null;
        MoveTo(DocumentStatus.Pending);
    }

    private void MoveTo(DocumentStatus status)
    {
        if (status != DocumentStatus.Failed)
        {
            FailureCode = null;
            FailureReason = null;
        }

        // Cleared on the way out of a pause as well as on the way in, so a document that has
        // resumed never shows a stale "resumes at" that has already passed.
        PauseReason = null;
        ResumesAt = null;

        Status = status;
    }
}
