using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Querio.Domain.Documents;
using Querio.Domain.Tenants;

namespace Querio.Infrastructure.Persistence.Configurations;

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");

        builder.HasKey(document => document.Id);

        builder.Property(document => document.FileName)
            .HasMaxLength(Document.MaxFileNameLength)
            .IsRequired();

        builder.Property(document => document.Format)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(document => document.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(document => document.ContentHash)
            .HasMaxLength(Document.ContentHashLength)
            .IsFixedLength()
            .IsRequired();

        builder.Property(document => document.StorageKey)
            .HasMaxLength(Document.MaxStorageKeyLength)
            .IsRequired();

        builder.Property(document => document.FailureCode)
            .HasMaxLength(Document.MaxFailureCodeLength);

        builder.Property(document => document.FailureReason)
            .HasMaxLength(Document.MaxFailureReasonLength);

        builder.Property(document => document.PauseReason)
            .HasMaxLength(Document.MaxPauseReasonLength);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(document => document.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // The same file uploaded twice is the same document. Enforced here rather than only
        // checked in the handler, because two concurrent uploads would both pass a check and
        // only a constraint can decide which one wins.
        builder.HasIndex(document => new { document.TenantId, document.ContentHash })
            .IsUnique();

        // The list screen is "this organization's documents, newest first", which this serves
        // directly. Id is UUID v7, so ordering by it is ordering by creation time.
        builder.HasIndex(document => new { document.TenantId, document.Id })
            .IsDescending(false, true);
    }
}
