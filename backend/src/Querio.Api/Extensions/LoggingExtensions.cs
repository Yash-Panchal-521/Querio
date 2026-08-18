using Serilog;
using Serilog.Events;

namespace Querio.Api.Extensions;

internal static class LoggingExtensions
{
    /// <summary>
    /// Wires Serilog from configuration, so sinks and levels are changed in appsettings
    /// rather than in code.
    /// </summary>
    public static WebApplicationBuilder AddQuerioLogging(this WebApplicationBuilder builder)
    {
        builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());

        return builder;
    }

    /// <summary>
    /// Replaces the framework's several-lines-per-request logging with one summary event
    /// per request, enriched with the fields worth querying on.
    /// </summary>
    public static WebApplication UseQuerioRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "{RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

            options.GetLevel = static (httpContext, _, exception) =>
            {
                if (exception is not null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError)
                {
                    return LogEventLevel.Error;
                }

                // Health probes run every few seconds; at Information they drown the log.
                return IsHealthProbe(httpContext) ? LogEventLevel.Verbose : LogEventLevel.Information;
            };

            options.EnrichDiagnosticContext = static (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());

                if (httpContext.User.Identity?.IsAuthenticated == true)
                {
                    diagnosticContext.Set("UserId", httpContext.User.Identity.Name);
                }
            };
        });

        return app;
    }

    private static bool IsHealthProbe(HttpContext httpContext) =>
        httpContext.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
}
