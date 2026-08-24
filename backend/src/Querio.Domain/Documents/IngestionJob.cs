using Querio.Domain.Common;

namespace Querio.Domain.Documents;

/// <summary>
/// One document's place in the ingestion queue.
///
/// A table rather than a broker: the job is created in the same transaction as the document it
/// describes, so a document can never exist without work queued for it, and no second system
/// has to be running for that guarantee to hold.
///
/// This carries <see cref="TenantId"/> but deliberately does not implement
/// <see cref="IHasTenant"/> — the same exception <c>Membership</c> makes, for the same reason.
/// The worker has no request and therefore no organization, so a filtered queue would be an
/// empty queue. Isolation is not lost: the worker adopts the tenant of the job it claimed
/// before it reads or writes anything the filter covers.
/// </summary>
public sealed class IngestionJob : Entity, IAuditable
{
    /// <summary>
    /// Bounded so a document that fails deterministically — a corrupt file, say — stops
    /// consuming the embedding allowance that working documents need.
    /// </summary>
    public const int MaxAttempts = 5;

    public const int MaxLastErrorLength = 1000;

    public const int MaxLeaseOwnerLength = 128;

    public const int MaxStorageKeyLength = 512;

    private IngestionJob()
    {
    }

    private IngestionJob(
        Guid tenantId,
        IngestionJobKind kind,
        Guid? documentId,
        string? storageKey,
        DateTimeOffset availableAt)
    {
        TenantId = tenantId;
        Kind = kind;
        DocumentId = documentId;
        StorageKey = storageKey;
        AvailableAt = availableAt;
        State = IngestionJobState.Queued;
    }

    public Guid TenantId { get; private set; }

    public IngestionJobKind Kind { get; private set; }

    /// <summary>Set for ingestion. Null for a cleanup job, whose document is already gone.</summary>
    public Guid? DocumentId { get; private set; }

    /// <summary>Set for cleanup. The object to remove.</summary>
    public string? StorageKey { get; private set; }

    public IngestionJobState State { get; private set; }

    public int Attempt { get; private set; }

    /// <summary>Not claimable before this. Backoff is expressed by pushing it forward.</summary>
    public DateTimeOffset AvailableAt { get; private set; }

    /// <summary>When the current claim lapses. Null unless leased.</summary>
    public DateTimeOffset? LeaseExpiresAt { get; private set; }

    /// <summary>Which worker holds it, for diagnosis rather than correctness.</summary>
    public string? LeasedBy { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static IngestionJob QueueIngestion(Guid tenantId, Guid documentId, DateTimeOffset now) =>
        new(tenantId, IngestionJobKind.IngestDocument, documentId, storageKey: null, now);

    /// <summary>
    /// Queued when a document's row is deleted but its object could not be. Carries the key
    /// rather than a document id, because there is no longer a document to point at.
    /// </summary>
    public static IngestionJob QueueObjectDeletion(Guid tenantId, string storageKey, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        return new IngestionJob(tenantId, IngestionJobKind.DeleteStoredObject, documentId: null, storageKey, now);
    }

    public void Lease(string owner, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        State = IngestionJobState.Leased;
        LeasedBy = owner;
        LeaseExpiresAt = expiresAt;
        Attempt++;
    }

    /// <summary>Extends a claim mid-flight, so slow work is not mistaken for a dead worker.</summary>
    public void RenewLease(DateTimeOffset expiresAt) => LeaseExpiresAt = expiresAt;

    public void Succeed()
    {
        State = IngestionJobState.Succeeded;
        LeaseExpiresAt = null;
        LeasedBy = null;
        LastError = null;
    }

    /// <summary>
    /// Returns the job to the queue, available again after <paramref name="availableAt"/>.
    /// Used for both a transient failure and a deliberate pause such as an exhausted daily
    /// allowance — the difference is how far out the caller pushes the time.
    /// </summary>
    public void Reschedule(DateTimeOffset availableAt, string? error)
    {
        State = IngestionJobState.Queued;
        AvailableAt = availableAt;
        LeaseExpiresAt = null;
        LeasedBy = null;
        LastError = Truncate(error);
    }

    /// <summary>
    /// Gives a paused job its attempt back. A daily allowance running out says nothing about
    /// whether the document is ingestible, so spending a retry on it would eventually fail a
    /// perfectly good file for a reason that was never its fault.
    /// </summary>
    public void PauseWithoutSpendingAnAttempt(DateTimeOffset availableAt, string reason)
    {
        if (Attempt > 0)
        {
            Attempt--;
        }

        Reschedule(availableAt, reason);
    }

    public void FailPermanently(string error)
    {
        State = IngestionJobState.Failed;
        LeaseExpiresAt = null;
        LeasedBy = null;
        LastError = Truncate(error);
    }

    public bool HasAttemptsLeft => Attempt < MaxAttempts;

    private static string? Truncate(string? error) =>
        error is null || error.Length <= MaxLastErrorLength
            ? error
            : error[..MaxLastErrorLength];
}
