namespace Querio.Domain.Documents;

/// <summary>
/// Where a unit of ingestion work stands, from the worker's point of view. The uploader sees
/// <see cref="DocumentStatus"/> instead.
/// </summary>
public enum IngestionJobState
{
    /// <summary>Waiting to be claimed. Eligible once <c>AvailableAt</c> has passed.</summary>
    Queued = 10,

    /// <summary>
    /// Claimed by a worker and held under a lease. If that worker dies, the lease expires and
    /// the job returns to the queue on its own — no operator, no cleanup job.
    /// </summary>
    Leased = 20,

    Succeeded = 30,

    /// <summary>Out of attempts. Terminal until someone re-uploads or explicitly retries.</summary>
    Failed = 40,
}
