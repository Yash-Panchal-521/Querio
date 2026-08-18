using System.Diagnostics;
using Querio.Api.Infrastructure.ExceptionHandling;

namespace Querio.Api.Extensions;

internal static class ExceptionHandlingExtensions
{
    /// <summary>
    /// Registers ProblemDetails plus the global handler. Every error response — thrown,
    /// or a bare status code with no body — leaves as ProblemDetails with a traceId.
    /// </summary>
    public static WebApplicationBuilder AddQuerioExceptionHandling(this WebApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                // Activity id ties the response to the distributed trace; TraceIdentifier is
                // the per-connection fallback when no listener is active.
                context.ProblemDetails.Extensions["traceId"] =
                    Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

                // Responses that never passed through the exception handler — a bare 404 from
                // routing, a 405 from a method mismatch — still need a code clients can branch
                // on. TryAdd leaves a handler-supplied code untouched.
                context.ProblemDetails.Extensions.TryAdd(
                    "errorCode",
                    DefaultErrorCodeFor(context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode));
            };
        });

        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        return builder;
    }

    /// <summary>
    /// Order matters: the exception handler must sit outermost so it wraps everything after
    /// it, and StatusCodePages fills in a body for bare 404/405 responses.
    /// </summary>
    public static WebApplication UseQuerioExceptionHandling(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        return app;
    }

    private static string DefaultErrorCodeFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "request.invalid",
        StatusCodes.Status401Unauthorized => "access.unauthorized",
        StatusCodes.Status403Forbidden => "access.forbidden",
        StatusCodes.Status404NotFound => "resource.not_found",
        StatusCodes.Status405MethodNotAllowed => "request.method_not_allowed",
        StatusCodes.Status406NotAcceptable => "request.not_acceptable",
        StatusCodes.Status409Conflict => "resource.conflict",
        StatusCodes.Status413PayloadTooLarge => "request.payload_too_large",
        StatusCodes.Status415UnsupportedMediaType => "request.unsupported_media_type",
        StatusCodes.Status429TooManyRequests => "quota.rate_limited",
        StatusCodes.Status503ServiceUnavailable => "server.unavailable",
        StatusCodes.Status504GatewayTimeout => "server.timeout",
        _ => statusCode >= StatusCodes.Status500InternalServerError
            ? "server.unexpected_error"
            : "request.rejected",
    };
}
