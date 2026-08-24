using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Querio.Domain.Documents;

namespace Querio.Infrastructure.Persistence.Configurations;

internal sealed class IngestionJobConfiguration : IEntityTypeConfiguration<IngestionJob>
{
    public void Configure(EntityTypeBuilder<IngestionJob> builder)
    {
        builder.ToTable("ingestion_jobs");

        builder.HasKey(job => job.Id);

        builder.Property(job => job.State)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(job => job.Kind)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(job => job.StorageKey)
            .HasMaxLength(IngestionJob.MaxStorageKeyLength);

        builder.Property(job => job.LeasedBy)
            .HasMaxLength(IngestionJob.MaxLeaseOwnerLength);

        builder.Property(job => job.LastError)
            .HasMaxLength(IngestionJob.MaxLastErrorLength);

        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(job => job.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // One live job per document. Without it, a retry racing a re-upload could queue the
        // same work twice and embed the same document twice — which spends the metered
        // resource, not just time. Filtered, because cleanup jobs carry no document and would
        // otherwise all collide on NULL.
        builder.HasIndex(job => job.DocumentId)
            .IsUnique()
            .HasFilter("document_id IS NOT NULL");

        // The claim query is "oldest queued job whose time has come", and it runs on a loop.
        // Filtered so the index stays the size of the backlog rather than of all history.
        builder.HasIndex(job => new { job.AvailableAt })
            .HasFilter($"state = {(int)IngestionJobState.Queued}");
    }
}
