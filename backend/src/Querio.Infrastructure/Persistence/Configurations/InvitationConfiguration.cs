using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Querio.Domain.Tenants;

namespace Querio.Infrastructure.Persistence.Configurations;

internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");

        builder.HasKey(invitation => invitation.Id);

        builder.Property(invitation => invitation.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(invitation => invitation.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(invitation => invitation.TokenHash)
            .HasMaxLength(32)
            .IsRequired();

        // Redemption looks the invitation up by this hash, so it must be indexed and unique.
        builder.HasIndex(invitation => invitation.TokenHash)
            .IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(invitation => invitation.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // At most one live invitation per address per organization. Filtered rather than a
        // plain unique index, so the same person can be re-invited after leaving.
        builder.HasIndex(invitation => new { invitation.TenantId, invitation.Email })
            .IsUnique()
            .HasFilter("accepted_at IS NULL AND revoked_at IS NULL");

        builder.HasIndex(invitation => invitation.TenantId);
    }
}
