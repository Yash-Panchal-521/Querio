using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Querio.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Querio.Api.Tests.Api;

/// <summary>
/// One Postgres container and one host for the whole test assembly.
///
/// A real database rather than an in-memory provider, because the things worth testing —
/// unique indexes, cascade behaviour, snake_case mapping, and later pgvector ranking — are
/// exactly the things an in-memory provider does not implement.
/// </summary>
public sealed class QuerioApiFixture : IAsyncLifetime
{
    // Match the production image now, so the first vector migration is not also the first
    // time this image is exercised.
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("querio")
        .WithUsername("querio")
        // Generated rather than written down. The value exists only in this process and the
        // throwaway container it starts, and a constant here is indistinguishable from a real
        // credential to anything scanning the repository.
        .WithPassword(Guid.NewGuid().ToString("N"))
        .Build();

    private QuerioApiFactory? factory;

    public QuerioApiFactory Factory =>
        factory ?? throw new InvalidOperationException("Fixture has not been initialised.");

    public TestTokenIssuer Tokens { get; } = new();

    public CapturingLogSink Logs { get; } = new();

    public string ConnectionString => container.GetConnectionString();

    public HttpClient CreateClient() => Factory.CreateClient();

    /// <summary>A client already carrying a valid token for the given identity.</summary>
    public HttpClient CreateAuthenticatedClient(
        string firebaseUid,
        string email = "user@example.com",
        bool emailVerified = true,
        string? displayName = "Test User")
    {
        var client = CreateClient();

        client.DefaultRequestHeaders.Authorization = new("Bearer",
            Tokens.IssueFor(firebaseUid, email, emailVerified, displayName));

        return client;
    }

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();

        factory = new QuerioApiFactory(ConnectionString, Tokens, Logs);

        // Apply migrations exactly as a deployment would, so the tests also prove the
        // migrations themselves run cleanly against an empty database.
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    /// <summary>Clears mutable state between tests without paying to recreate the container.</summary>
    public async Task ResetAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE users RESTART IDENTITY CASCADE;");
    }

    public async ValueTask DisposeAsync()
    {
        if (factory is not null)
        {
            await factory.DisposeAsync();
        }

        Tokens.Dispose();

        await container.DisposeAsync();
    }
}

/// <summary>
/// Assembly-wide collection: starting a container per test class would add tens of seconds
/// for no isolation benefit, since <see cref="QuerioApiFixture.ResetAsync"/> handles state.
/// </summary>
[CollectionDefinition(nameof(QuerioApiCollection))]
public sealed class QuerioApiCollection : ICollectionFixture<QuerioApiFixture>;
