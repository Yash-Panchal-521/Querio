using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace Querio.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c>. Having it means migrations can be created and scripted
/// without a running database or a populated configuration file — the design-time connection
/// string is never used to connect.
/// </summary>
internal sealed class QuerioDbContextFactory : IDesignTimeDbContextFactory<QuerioDbContext>
{
    // Deliberately credential-free: this is parsed, never connected with. `migrations add`
    // only builds the model, so a syntactically valid string is enough. Anything that does
    // reach the database — `database update` — has to supply QUERIO_CONNECTION_STRING, and
    // failing loudly beats silently targeting whatever happens to sit on the default port.
    private const string DesignTimeConnectionString = "Host=localhost;Database=querio";

    public QuerioDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("QUERIO_CONNECTION_STRING")
            ?? DesignTimeConnectionString;

        var options = new DbContextOptionsBuilder<QuerioDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(QuerioDbContext).Assembly.FullName);
                npgsql.UseVector();
            })
            .UseSnakeCaseNamingConvention()
            .Options;

        return new QuerioDbContext(options, NoTenantContext.Instance);
    }
}
