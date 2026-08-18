using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Querio.Domain.Tenants;
using Querio.Domain.Users;

namespace Querio.Infrastructure.Persistence.Configurations;

internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("memberships");

        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.Role)
            .HasConversion<int>()
            .IsRequired();

        // One membership per person per organization. Without this a retried invitation
        // acceptance could grant someone two roles at once.
        builder.HasIndex(membership => new { membership.TenantId, membership.UserId })
            .IsUnique();

        // "Which organizations do I belong to" runs on every sign-in.
        builder.HasIndex(membership => membership.UserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
