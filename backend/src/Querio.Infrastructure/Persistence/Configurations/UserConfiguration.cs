using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Querio.Domain.Users;

namespace Querio.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.FirebaseUid)
            .HasMaxLength(128)
            .IsRequired();

        // The real identity. Two sign-in methods for one human are two uids, hence two rows.
        builder.HasIndex(user => user.FirebaseUid)
            .IsUnique();

        builder.Property(user => user.Email)
            .HasMaxLength(320) // RFC 5321 maximum path length.
            .IsRequired();

        // Deliberately NOT unique: see the account-linking limitation. Indexed because
        // invitations are matched by email address.
        builder.HasIndex(user => user.Email);

        builder.Property(user => user.EmailVerified)
            .IsRequired();

        builder.Property(user => user.DisplayName)
            .HasMaxLength(200);

        builder.Property(user => user.CreatedAt).IsRequired();
        builder.Property(user => user.UpdatedAt).IsRequired();
    }
}
