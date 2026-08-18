using Microsoft.Extensions.Logging;

namespace Querio.Application.Common.Behaviors;

/// <summary>
/// Source-generated log methods: the level check happens before any argument is formatted,
/// and the event ids stay stable for alerting.
/// </summary>
internal static partial class RequestLoggingBehaviorLog
{
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Handled {MessageType} in {ElapsedMilliseconds} ms")]
    public static partial void MessageHandled(
        this ILogger logger,
        string messageType,
        long elapsedMilliseconds);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "{MessageType} rejected with {ErrorCode} after {ElapsedMilliseconds} ms")]
    public static partial void MessageFailed(
        this ILogger logger,
        string messageType,
        string errorCode,
        long elapsedMilliseconds);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Error,
        Message = "{MessageType} faulted after {ElapsedMilliseconds} ms")]
    public static partial void MessageFaulted(
        this ILogger logger,
        Exception exception,
        string messageType,
        long elapsedMilliseconds);
}
