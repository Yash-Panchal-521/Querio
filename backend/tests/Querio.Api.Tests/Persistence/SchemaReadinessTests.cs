using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Querio.Api.Tests.Api;
using Querio.Infrastructure.Persistence;

namespace Querio.Api.Tests.Persistence;

/// <summary>
/// The readiness probe is polled continuously and used to hit the database every time, which
/// is enough on its own to stop an idle-suspending database ever suspending. These tests pin
/// both halves of the fix: a confirmed schema stops querying, and a failure never does.
/// </summary>
[Collection(nameof(QuerioApiCollection))]
public sealed class SchemaReadinessTests(QuerioApiFixture fixture)
{
    /// <summary>Points at a closed port, so any attempt to connect fails immediately.</summary>
    private static QuerioDbContext UnreachableDatabase() =>
        new(
            new DbContextOptionsBuilder<QuerioDbContext>()
                .UseNpgsql("Host=127.0.0.1;Port=1;Database=absent;Username=absent;Password=absent")
                .Options,
            NoTenantContext.Instance);

    [Fact]
    public async Task Once_the_schema_is_confirmed_the_probe_stops_querying()
    {
        var cache = new SchemaReadinessCache();

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var live = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

        var first = await new PendingMigrationsHealthCheck(live, cache)
            .CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        first.Status.ShouldBe(HealthStatus.Healthy);
        cache.IsConfirmed.ShouldBeTrue();

        // The second check is handed a database that cannot be reached at all. Reporting
        // healthy anyway is only possible if it never opened a connection — which is the
        // behaviour the free-tier compute allowance depends on.
        await using var unreachable = UnreachableDatabase();

        var second = await new PendingMigrationsHealthCheck(unreachable, cache)
            .CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        second.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task An_unreachable_database_is_never_cached_as_ready()
    {
        var cache = new SchemaReadinessCache();

        await using var unreachable = UnreachableDatabase();

        var result = await new PendingMigrationsHealthCheck(unreachable, cache)
            .CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);

        // Caching a failure would leave the instance permanently unready after one bad moment,
        // with a restart as the only cure.
        cache.IsConfirmed.ShouldBeFalse();
    }
}
