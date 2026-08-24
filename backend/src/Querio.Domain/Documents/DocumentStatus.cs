namespace Querio.Domain.Documents;

/// <summary>
/// How far a document has got, as the person who uploaded it would describe it.
///
/// This is the user-facing projection of ingestion, deliberately separate from
/// <see cref="IngestionJobState"/>: one answers "can I search this yet", the other answers
/// "should a worker pick this up". Collapsing them would put lease expiry and retry counts in
/// front of someone who only wanted to know whether their file was ready.
/// </summary>
public enum DocumentStatus
{
    /// <summary>Stored and queued. Nothing has read it yet.</summary>
    Pending = 10,

    /// <summary>Pulling text out of the original file.</summary>
    Extracting = 20,

    /// <summary>Splitting the extracted text into passages.</summary>
    Chunking = 30,

    /// <summary>Passages exist; embeddings are being generated for them.</summary>
    Embedding = 40,

    /// <summary>
    /// Paused because the embedding provider's daily allowance is spent. Distinct from
    /// <see cref="Failed"/> because nothing is wrong and no action is needed — it resumes.
    /// </summary>
    WaitingForQuota = 50,

    /// <summary>Every passage is embedded. The document is searchable.</summary>
    Ready = 60,

    /// <summary>Gave up. <c>FailureReason</c> says why in words a person can act on.</summary>
    Failed = 70,
}
