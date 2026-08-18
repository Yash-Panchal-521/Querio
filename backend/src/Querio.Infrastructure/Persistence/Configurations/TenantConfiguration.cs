using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Querio.Domain.Tenants;
using Querio.Domain.Users;

namespace Querio.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(tenant => tenant.Id);

        builder.Property(tenant => tenant.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(tenant => tenant.Slug)
            .HasMaxLength(48)
            .IsRequired();

        // Slugs appear in URLs, so collisions must be impossible rather than unlikely.
        builder.HasIndex(tenant => tenant.Slug)
            .IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(tenant => tenant.CreatedByUserId)
            // Restrict, not Cascade: deleting the person who happened to create an
            // organization must never delete the organization out from under its members.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(tenant => tenant.Memberships)
            .WithOne()
            .HasForeignKey(membership => membership.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(tenant => tenant.Memberships)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("memberships");
    }
}
