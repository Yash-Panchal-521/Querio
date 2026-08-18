using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Common;
using Querio.Domain.Tenants;
using Querio.Domain.Users;

namespace Querio.Infrastructure.Persistence;

public sealed class QuerioDbContext(DbContextOptions<QuerioDbContext> options, ITenantContext tenantContext)
    : DbContext(options), IQuerioDbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    /// <summary>
    /// Read through a property rather than captured, so EF turns it into a query parameter
    /// re-evaluated per request instead of baking the first request's tenant into the model.
    /// </summary>
    private Guid? CurrentTenantId => tenantContext.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Ids are generated in the domain as UUID v7, never by the database. Saying so stops
        // EF treating a set key as proof the row already exists — the assumption that makes it
        // emit UPDATE for a brand-new child added to a tracked aggregate.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(type => typeof(Entity).IsAssignableFrom(type.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(Entity.Id))
                .ValueGeneratedNever();
        }

        // Tenant-owned data is default-deny: with no established tenant the filter matches
        // nothing at all, rather than falling open. A query that must legitimately cross
        // organizations — redeeming an invitation before you are a member — has to say
        // IgnoreQueryFilters explicitly, which makes every crossing visible in review.
        //
        // Every IHasTenant entity must be listed here. QueryFilterTests fails the build if one
        // is missed, because a forgotten filter is silent.
        modelBuilder.Entity<Invitation>()
            .HasQueryFilter(invitation => CurrentTenantId != null && invitation.TenantId == CurrentTenantId);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<string>().AreUnicode();
    }
}
