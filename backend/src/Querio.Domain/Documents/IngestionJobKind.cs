namespace Querio.Domain.Documents;

/// <summary>
/// What a queued unit of work is for.
///
/// The queue carries more than ingestion because the alternative was a second queue with the
/// same leasing, the same backoff and the same crash-safety, built again. Deleting a document
/// removes its row first and its stored object second — that order is deliberate, since the
/// reverse would leave a document listed whose bytes are gone — and a failure in between needs
/// somewhere to be retried from rather than only a line in a log.
/// </summary>
public enum IngestionJobKind
{
    /// <summary>Extract, chunk and embed an uploaded document.</summary>
    IngestDocument = 10,

    /// <summary>
    /// Remove an object whose document row is already gone. Nothing references it, so it is
    /// invisible in the product and costs storage until something collects it.
    /// </summary>
    DeleteStoredObject = 20,
}
