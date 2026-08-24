namespace Querio.Application.Common.Abstractions;

/// <summary>
/// Where the uploaded bytes actually live.
///
/// Not the database. Postgres is the small, expensive, size-limited store and the free plan
/// caps it at half a gigabyte — a store that has to hold chunk text and vectors has no business
/// also holding the originals those were derived from.
///
/// Storage owns its own naming. Callers hand over a tenant and a content hash and are told the
/// key that resulted, rather than composing paths themselves, so the layout can change without
/// touching a single use case.
/// </summary>
public interface IDocumentStorage
{
    /// <summary>
    /// Stores the content and returns the key it was stored under.
    ///
    /// Writing the same tenant and hash twice is deliberately harmless: the key is derived from
    /// the content, so a retry overwrites bytes that were already identical.
    /// </summary>
    Task<string> StoreAsync(
        Guid tenantId,
        string contentHash,
        Stream content,
        string contentType,
        CancellationToken cancellationToken);

    /// <summary>Opens the stored content for reading. The caller disposes the stream.</summary>
    Task<Stream> OpenAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the object. Deleting something already gone is not an error — a delete that
    /// retried after half-succeeding must be able to finish.
    /// </summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>
    /// A time-limited link the browser can follow directly, so file bytes never pass through
    /// the API. The bucket itself stays private.
    /// </summary>
    Task<Uri> CreateDownloadLinkAsync(
        string storageKey,
        string downloadFileName,
        TimeSpan lifetime,
        CancellationToken cancellationToken);
}
