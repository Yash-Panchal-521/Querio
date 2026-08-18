using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Querio.Infrastructure.Persistence;

/// <summary>
/// Reports the instance unready while the schema is behind the code.
///
/// Migrations are applied as a deliberate deploy step, never by calling Migrate() on start-up
/// — with more than one instance that races, and a half-migrated schema is worse than a
/// stopped rollout. This check is what makes the deliberate step safe: an instance whose
/// schema is stale never receives traffic.
/// </summary>
internal sealed class PendingMigrationsHealthCheck(QuerioDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

            if (pending.Length > 0)
            {
                return HealthCheckResult.Unhealthy(
                    $"Database schema is behind the application. Pending migrations: {string.Join(", ", pending)}.");
            }

            return HealthCheckResult.Healthy("Database reachable and schema up to date.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Database is unreachable.", exception);
        }
    }
}
