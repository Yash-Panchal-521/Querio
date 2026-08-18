namespace Querio.Api.Infrastructure.ExceptionHandling;

/// <summary>
/// Source-generated log methods. The generator emits the level check before any argument is
/// formatted, so a disabled level costs nothing — and the event ids stay stable for alerting.
/// </summary>
internal static partial class GlobalExceptionHandlerLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Request {RequestMethod} {RequestPath} was cancelled by the client")]
    public static partial void RequestCancelled(
        this ILogger logger,
        string requestMethod,
        string requestPath);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "Unhandled {ErrorCode} on {RequestMethod} {RequestPath}")]
    public static partial void UnhandledException(
        this ILogger logger,
        Exception exception,
        string errorCode,
        string requestMethod,
        string requestPath);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Request rejected with {ErrorCode} ({StatusCode}) on {RequestMethod} {RequestPath}: {Reason}")]
    public static partial void RequestRejected(
        this ILogger logger,
        string errorCode,
        int statusCode,
        string requestMethod,
        string requestPath,
        string reason);
}
