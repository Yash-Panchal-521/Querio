using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Querio.Domain.Documents;
using Querio.Infrastructure.Persistence.Converters;

namespace Querio.Infrastructure.Persistence.Configurations;

internal sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    /// <summary>
    /// Graph degree. 16 is pgvector's default and the usual recommendation: higher improves
    /// recall and costs both build time and index size, and index size is the constraint that
    /// binds here.
    /// </summary>
    private const int HnswConnections = 16;

    /// <summary>
    /// Candidate list size while building. 64 is the default; raising it buys recall at the
    /// cost of a slower build, which on a database with 0.25 vCPU is not free.
    /// </summary>
    private const int HnswBuildCandidates = 64;

    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("document_chunks");

        builder.HasKey(chunk => chunk.Id);

        builder.Property(chunk => chunk.Text)
            .IsRequired();

        builder.Property(chunk => chunk.Breadcrumb)
            .HasMaxLength(DocumentChunk.MaxBreadcrumbLength);

        builder.Property(chunk => chunk.Embedding)
            .HasColumnType($"halfvec({DocumentChunk.EmbeddingDimensions})")
            .HasConversion(EmbeddingConversion.Converter, EmbeddingConversion.Comparer);

        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(chunk => chunk.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Re-ingestion rewrites a document's chunks. The constraint is what makes that safe:
        // a retry that raced itself would otherwise leave two chunks claiming the same place.
        builder.Property(chunk => chunk.EmbeddingModel)
            .HasMaxLength(DocumentChunk.MaxEmbeddingModelLength);

        builder.HasIndex(chunk => new { chunk.DocumentId, chunk.Ordinal })
            .IsUnique();

        builder.HasIndex(chunk => chunk.TenantId);

        // Cosine, because the embeddings are L2-normalised — gemini-embedding-001 requires
        // manual normalisation below its native 3072 dimensions, so we do it before storing.
        builder.HasIndex(chunk => chunk.Embedding)
            .HasMethod("hnsw")
            .HasOperators("halfvec_cosine_ops")
            .HasStorageParameter("m", HnswConnections)
            .HasStorageParameter("ef_construction", HnswBuildCandidates);
    }
}
