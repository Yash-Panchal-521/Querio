using Querio.Domain.Common;

namespace Querio.Domain.Documents;

/// <summary>
/// One passage of a document, and the vector that makes it findable.
///
/// Chunks keep where they came from — ordinal, character offsets, page, and the heading path
/// above them. That is what turns a retrieved fragment into a citation someone can follow back
/// to the source, instead of a context-free paragraph the reader has to take on trust.
/// </summary>
public sealed class DocumentChunk : Entity, IAuditable, IHasTenant
{
    /// <summary>
    /// Fixed at the column, so changing it is a migration and a re-embed, never a config
    /// change. 768 is a truncation of the model's native 3072 that Google supports directly,
    /// and it matches bge-base and nomic-embed-text — so moving to a local model later stays a
    /// re-embed rather than a schema change.
    /// </summary>
    public const int EmbeddingDimensions = 768;

    public const int MaxBreadcrumbLength = 512;

    /// <summary>Room for a model name and the dimensionality it was asked for.</summary>
    public const int MaxEmbeddingModelLength = 128;

    private DocumentChunk()
    {
        Text = string.Empty;
    }

    private DocumentChunk(
        Guid tenantId,
        Guid documentId,
        int ordinal,
        string text,
        string? breadcrumb,
        int? pageNumber,
        int startOffset,
        int endOffset,
        int approximateTokenCount)
    {
        TenantId = tenantId;
        DocumentId = documentId;
        Ordinal = ordinal;
        Text = text;
        Breadcrumb = breadcrumb;
        PageNumber = pageNumber;
        StartOffset = startOffset;
        EndOffset = endOffset;
        ApproximateTokenCount = approximateTokenCount;
    }

    public Guid TenantId { get; private set; }

    public Guid DocumentId { get; private set; }

    /// <summary>Position within the document, from zero. Unique per document.</summary>
    public int Ordinal { get; private set; }

    public string Text { get; private set; }

    /// <summary>
    /// The heading path above this passage — "Handbook › Leave › Parental". Null where the
    /// format has no headings to read, such as a PDF whose structure is only visual.
    /// </summary>
    public string? Breadcrumb { get; private set; }

    /// <summary>Set for paged formats only.</summary>
    public int? PageNumber { get; private set; }

    /// <summary>Character offsets into the extracted text, for exact highlighting later.</summary>
    public int StartOffset { get; private set; }

    public int EndOffset { get; private set; }

    /// <summary>
    /// Approximate by construction — an exact count would mean shipping a vocabulary or
    /// spending an API call per chunk. Chunks are sized far enough below the model's ceiling
    /// that the approximation cannot push one over it, and the interface labels it as such.
    /// </summary>
    public int ApproximateTokenCount { get; private set; }

    /// <summary>
    /// Null until embedded. Deliberately a plain array rather than a database-specific vector
    /// type: Domain must not know that Postgres, or pgvector, is what stores this. The mapping
    /// to <c>halfvec</c> lives in Infrastructure.
    /// </summary>
    public float[]? Embedding { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static DocumentChunk Create(
        Guid tenantId,
        Guid documentId,
        int ordinal,
        string text,
        string? breadcrumb,
        int? pageNumber,
        int startOffset,
        int endOffset,
        int approximateTokenCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentOutOfRangeException.ThrowIfNegative(startOffset);
        ArgumentOutOfRangeException.ThrowIfLessThan(endOffset, startOffset);

        return new DocumentChunk(
            tenantId,
            documentId,
            ordinal,
            text,
            breadcrumb,
            pageNumber,
            startOffset,
            endOffset,
            approximateTokenCount);
    }

    /// <summary>
    /// Which model produced <see cref="Embedding"/>, as a stable identifier including the
    /// dimensionality it was asked for — <c>nomic-embed-text-v1.5@768</c>.
    ///
    /// Null only for vectors written before this was recorded. Retrieval filters on it, because
    /// two models agreeing on dimensionality does not make their vectors comparable: cosine
    /// distance between different embedding spaces is noise, and nothing would report it. The
    /// column turns a silent loss of relevance into an explicit compatibility boundary.
    /// </summary>
    public string? EmbeddingModel { get; private set; }

    public void AttachEmbedding(float[] embedding, string embeddingModel)
    {
        ArgumentNullException.ThrowIfNull(embedding);
        ArgumentException.ThrowIfNullOrWhiteSpace(embeddingModel);

        if (embedding.Length != EmbeddingDimensions)
        {
            // The column is fixed width, so a wrong length fails at the database with an
            // error that says nothing about which provider or setting produced it. Catching
            // it here names the actual problem: something returned the wrong dimensionality.
            throw new ArgumentException(
                $"Expected {EmbeddingDimensions} dimensions but got {embedding.Length}.",
                nameof(embedding));
        }

        Embedding = embedding;
        EmbeddingModel = embeddingModel;
    }
}
