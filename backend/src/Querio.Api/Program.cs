using System.Globalization;
using System.Reflection;
using Querio.Api.Common.Endpoints;
using Querio.Api.Extensions;
using Querio.Application;
using Querio.Infrastructure;
using Serilog;

// Bootstrap logger: anything that fails while the host is still being built (bad config,
// missing connection string) would otherwise be swallowed and surface as a silent exit.
// Invariant culture explicitly: log output is parsed by machines, so a server running under
// a locale with different date or number formatting must not produce different log text.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Local development settings, git-ignored and optional. Added last so it overrides the
    // environment file, and absent everywhere but a developer's machine — which is why it is
    // safe to keep real credentials in it and why nothing here depends on it existing.
    builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

    builder.AddQuerioLogging();
    builder.AddQuerioJson();
    builder.AddQuerioExceptionHandling();
    builder.AddQuerioCors();
    builder.AddQuerioAuthentication();
    builder.AddQuerioAuthorization();
    builder.AddQuerioRateLimiting();
    builder.AddQuerioOpenApi();

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseQuerioExceptionHandling();
    app.UseQuerioRequestLogging();
    app.UseCors(CorsExtensions.PolicyName);

    // Authentication must precede authorization, and both must precede endpoint execution.
    app.UseAuthentication();
    app.UseAuthorization();

    // After authentication, so limits partition on the authenticated subject rather than
    // lumping everyone behind one NAT together.
    app.UseRateLimiter();

    app.UseQuerioOpenApi();
    app.MapEndpoints();

    await app.RunAsync();

    return 0;
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "Querio API terminated unexpectedly");

    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Exposed so <c>WebApplicationFactory</c> can boot the real pipeline in tests.</summary>
public partial class Program;
