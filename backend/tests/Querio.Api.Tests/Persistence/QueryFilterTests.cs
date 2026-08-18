using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Querio.Api.Tests.Api;
using Querio.Domain.Common;
using Querio.Domain.Tenants;
using Querio.Infrastructure.Persistence;

namespace Querio.Api.Tests.Persistence;

/// <summary>
/// A missing tenant filter is silent: queries keep working and quietly return other
/// organizations' rows. Nothing about adding an entity forces you to remember, so this does.
/// </summary>
[Collection(nameof(QuerioApiCollection))]
public sealed class QueryFilterTests(QuerioApiFixture fixture)
{
    [Fact]
    public void Every_tenant_owned_entity_is_filtered()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

        var unfiltered = dbContext.Model.GetEntityTypes()
            .Where(entityType => typeof(IHasTenant).IsAssignableFrom(entityType.ClrType))
            .Where(entityType => entityType.GetDeclaredQueryFilters().Count == 0)
            .Select(entityType => entityType.ClrType.Name)
            .ToArray();

        unfiltered.ShouldBeEmpty(
            $"These entities own tenant data but are not filtered: {string.Join(", ", unfiltered)}");
    }

    [Fact]
    public void The_filter_check_is_actually_looking_at_something()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

        // Without this, the test above passes trivially the moment IHasTenant stops matching
        // anything — for example after a namespace move.
        var tenantOwned = dbContext.Model.GetEntityTypes()
            .Where(entityType => typeof(IHasTenant).IsAssignableFrom(entityType.ClrType))
            .Select(entityType => entityType.ClrType)
            .ToArray();

        tenantOwned.ShouldContain(typeof(Invitation));
    }

    [Fact]
    public void Membership_is_deliberately_not_filtered()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

        // Memberships decide tenant access, and "which organizations do I belong to" is
        // inherently cross-tenant. Filtering it would make authorization self-referential and
        // break the organization switcher.
        var membership = dbContext.Model.FindEntityType(typeof(Membership));

        membership.ShouldNotBeNull();
        membership.GetDeclaredQueryFilters().ShouldBeEmpty();
    }
}
