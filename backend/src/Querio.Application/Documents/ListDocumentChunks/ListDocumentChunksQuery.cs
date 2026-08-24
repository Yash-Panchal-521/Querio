using Mediator;

namespace Querio.Application.Documents.ListDocumentChunks;

/// <summary>
/// Paged, because a long document produces hundreds of passages and the inspector shows them
/// as a list somebody scrolls rather than a page they wait for.
/// </summary>
public sealed record ListDocumentChunksQuery(Guid TenantId, Guid DocumentId, int Skip, int Take)
    : IQuery<DocumentChunkPage>;

/// <param name="Total">Across the whole document, so the interface can say "50 of 312".</param>
public sealed record DocumentChunkPage(IReadOnlyList<DocumentChunkView> Chunks, int Total);

/// <param name="HasEmbedding">
/// Whether this passage is searchable yet. The vector itself is never sent — three kilobytes
/// per passage that no interface can do anything with.
/// </param>
public sealed record DocumentChunkView(
    Guid Id,
    int Ordinal,
    string Text,
    string? Breadcrumb,
    int? PageNumber,
    int ApproximateTokenCount,
    bool HasEmbedding);
