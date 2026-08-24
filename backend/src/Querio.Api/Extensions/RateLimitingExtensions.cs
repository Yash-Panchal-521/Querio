using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Querio.Api.Common.Authentication;
using Querio.Api.Common.RateLimiting;

namespace Querio.Api.Extensions;

internal static class RateLimitingExtensions
{
    public static WebApplicationBuilder AddQuerioRateLimiting(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<RateLimitingOptions>()
            .Bind(builder.Configuration.GetSection(RateLimitingOptions.SectionName));

        var limits = builder.Configuration
            .GetSection(RateLimitingOptions.SectionName)
            .Get<RateLimitingOptions>() ?? new RateLimitingOptions();

        builder.Services.AddRateLimiter(options =>
        {
            options.AddPolicy(RateLimitPolicies.Bootstrap, httpContext =>
                PartitionByCaller(httpContext, limits.Bootstrap));

            options.AddPolicy(RateLimitPolicies.InvitationRedemption, httpContext =>
                PartitionByCaller(httpContext, limits.InvitationRedemption));

            options.AddPolicy(RateLimitPolicies.DocumentUpload, httpContext =>
                PartitionByCaller(httpContext, limits.DocumentUpload));

            options.OnRejected = WriteRejectionAsync;
        });

        return builder;
    }

    /// <summary>
    /// Partitions on the authenticated subject, falling back to the remote address. Anyone
    /// can mint a valid token in their own Firebase project, so the token proves identity but
    /// not trustworthiness — the limit has to apply per caller regardless.
    /// </summary>
    private static RateLimitPartition<string> PartitionByCaller(HttpContext httpContext, RateLimitWindow window)
    {
        var subject = httpContext.User.FindFirst(FirebaseClaims.Subject)?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(subject, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = window.PermitLimit,
            Window = window.Window,
            // No queue: a caller past the limit should be told immediately, not held on a
            // connection until a slot frees.
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        // Without this the client has no idea whether to retry in a second or an hour.
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

        // Written here rather than left to StatusCodePages, so the response carries the same
        // shape — errorCode and traceId — as every other failure the API produces.
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = ReasonPhrases.GetReasonPhrase(StatusCodes.Status429TooManyRequests),
                Detail = "Too many requests. Wait a moment and try again.",
                Type = "https://httpstatuses.io/429",
                Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}",
            },
        });
    }
}
