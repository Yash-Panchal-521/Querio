namespace Querio.Domain.Documents;

/// <summary>
/// What one organization may store.
///
/// These are not arbitrary. The database this runs on allows half a gigabyte in total and the
/// object store ten, and both are shared by every organization — so a limit that only appears
/// when the platform runs out is a limit that arrives as an outage. Refusing early, with a
/// sentence explaining what to remove, is the difference between a boundary and a failure.
/// </summary>
public static class DocumentLimits
{
    /// <summary>
    /// Per file. Large enough for a book-length PDF, small enough that one upload cannot
    /// occupy the whole instance's disk while it is being hashed.
    /// </summary>
    public const long MaxFileBytes = 20L * 1024 * 1024;

    public const int MaxDocumentsPerTenant = 200;

    /// <summary>
    /// Total original bytes per organization. The object store is the generous side of the
    /// budget; the binding constraint is the chunks and vectors these produce.
    /// </summary>
    public const long MaxStoredBytesPerTenant = 500L * 1024 * 1024;

    /// <summary>
    /// How long a download link stays valid. Long enough to click, short enough that a link
    /// pasted somewhere public has usually expired before it is useful to anyone.
    ///
    /// Here rather than in the storage settings so there is one source of truth: a lifetime
    /// configured in two places eventually disagrees, and the shorter one wins silently.
    /// </summary>
    public static readonly TimeSpan DownloadLinkLifetime = TimeSpan.FromMinutes(10);
}
