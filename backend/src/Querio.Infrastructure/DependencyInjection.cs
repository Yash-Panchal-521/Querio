using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Querio.Application.Common.Abstractions;
using Querio.Infrastructure.Persistence;
using Querio.Infrastructure.Persistence.Interceptors;

namespace Querio.Infrastructure;

public static class DependencyInjection
{
    private const string ConnectionStringName = "Querio";

    /// <summary>
    /// Composition root for everything that talks to the outside world — Postgres, object
    /// storage, the embedding and chat providers, and the ingestion worker.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Fail at start-up with a sentence that says what to do, rather than at the first
            // request with a null-reference somewhere inside EF.
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. Set ConnectionStrings:{ConnectionStringName} "
                + "in configuration, user secrets, or the environment.");
        }

        services.TryAddSingletonTimeProvider();

        services.AddSingleton<AuditableEntityInterceptor>();

        services.AddDbContext<QuerioDbContext>((serviceProvider, options) =>
        {
            options
                .UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(QuerioDbContext).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
                })
                // Postgres is case-folding and snake_case by convention; matching it keeps
                // hand-written SQL and psql sessions free of quoted identifiers.
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>());
        });

        services.AddScoped<IQuerioDbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<QuerioDbContext>());

        services.AddHealthChecks()
            .AddCheck<PendingMigrationsHealthCheck>(
                name: "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);

        return services;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        // Tests swap in FakeTimeProvider; nothing else should construct DateTimeOffset.UtcNow.
        if (services.All(descriptor => descriptor.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
