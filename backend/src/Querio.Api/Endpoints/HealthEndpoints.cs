using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Querio.Api.Common.Endpoints;

namespace Querio.Api.Endpoints;

internal sealed class HealthEndpoints : IEndpoint
{
    /// <summary>
    /// Two probes, because they answer different questions: liveness asks "is the process
    /// wedged, restart it?", readiness asks "can it serve traffic right now?". Dependency
    /// checks (Postgres, object storage, the model provider) register into readiness only.
    /// </summary>
    public void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = static _ => false,
        }).ExcludeFromDescription();

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = static registration => registration.Tags.Contains("ready"),
            ResponseWriter = WriteHealthResponseAsync,
        }).ExcludeFromDescription();
    }

    private static Task WriteHealthResponseAsync(HttpContext httpContext, HealthReport report)
    {
        httpContext.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = entry.Value.Duration.TotalMilliseconds,
                description = entry.Value.Description,
            }),
        };

        return httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(payload, JsonSerializerOptions.Web),
            httpContext.RequestAborted);
    }
}
