using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Querio.Domain.Common.Errors;

namespace Querio.Api.Infrastructure.ExceptionHandling;

/// <summary>
/// Terminal handler for anything that escapes an endpoint. Every failure leaves the API as
/// RFC 9457 ProblemDetails carrying a traceId, so a user-reported error can be tied back to
/// a log line without guesswork.
///
/// This is the only place that knows how a domain <see cref="ErrorCategory"/> maps to an
/// HTTP status; keeping that translation here is what lets Domain and Application stay free
/// of any ASP.NET reference.
/// </summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private const string UnexpectedErrorDetail =
        "An unexpected error occurred. Quote the traceId when reporting this.";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var method = httpContext.Request.Method;
        var path = httpContext.Request.Path.ToString();

        // The client hung up mid-request. Nothing to write to, and it is not a fault.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.RequestCancelled(method, path);

            return true;
        }

        var error = Describe(exception, environment);

        if (error.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.UnhandledException(exception, error.ErrorCode, method, path);
        }
        else
        {
            logger.RequestRejected(error.ErrorCode, error.StatusCode, method, path, exception.Message);
        }

        // Must be set before the body is written, otherwise the status is already on the wire.
        httpContext.Response.StatusCode = error.StatusCode;

        var problemDetails = new ProblemDetails
        {
            Status = error.StatusCode,
            Title = ReasonPhrases.GetReasonPhrase(error.StatusCode),
            Detail = error.Detail,
            Type = $"https://httpstatuses.io/{error.StatusCode}",
            Instance = $"{method} {path}",
        };

        problemDetails.Extensions["errorCode"] = error.ErrorCode;

        if (error.Extensions is not null)
        {
            foreach (var (key, value) in error.Extensions)
            {
                problemDetails.Extensions[key] = value;
            }
        }

        // TryWriteAsync also runs CustomizeProblemDetails, which stamps the traceId.
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });
    }

    internal static int StatusCodeFor(ErrorCategory category) => category switch
    {
        ErrorCategory.Validation => StatusCodes.Status400BadRequest,
        ErrorCategory.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorCategory.Forbidden => StatusCodes.Status403Forbidden,
        ErrorCategory.NotFound => StatusCodes.Status404NotFound,
        ErrorCategory.Conflict => StatusCodes.Status409Conflict,
        ErrorCategory.RateLimited => StatusCodes.Status429TooManyRequests,
        ErrorCategory.Unavailable => StatusCodes.Status503ServiceUnavailable,
        ErrorCategory.Timeout => StatusCodes.Status504GatewayTimeout,
        _ => StatusCodes.Status500InternalServerError,
    };

    private static ErrorDescription Describe(Exception exception, IHostEnvironment environment) =>
        exception switch
        {
            QuerioException known => new ErrorDescription(
                StatusCodeFor(known.Category),
                known.ErrorCode,
                known.Message,
                known.Extensions),

            // Malformed body, bad route value, payload over the size limit.
            BadHttpRequestException badRequest => new ErrorDescription(
                badRequest.StatusCode,
                "request.malformed",
                badRequest.Message,
                Extensions: null),

            // A dependency timed out or the work was abandoned server-side.
            TimeoutException or OperationCanceledException => new ErrorDescription(
                StatusCodes.Status504GatewayTimeout,
                "server.timeout",
                "The request took too long to complete. Try again.",
                Extensions: null),

            // Never leak a stack trace to a production caller — it goes to the log instead.
            _ => new ErrorDescription(
                StatusCodes.Status500InternalServerError,
                "server.unexpected_error",
                environment.IsDevelopment() ? exception.ToString() : UnexpectedErrorDetail,
                Extensions: null),
        };

    private readonly record struct ErrorDescription(
        int StatusCode,
        string ErrorCode,
        string Detail,
        IReadOnlyDictionary<string, object?>? Extensions);
}
