using System.Diagnostics;
using Mediator;
using Microsoft.Extensions.Logging;
using Querio.Domain.Common.Errors;

namespace Querio.Application.Common.Behaviors;

/// <summary>
/// One log event per dispatched message, with its duration. Deliberate failures log as
/// warnings with their error code; anything unrecognised logs as an error with the stack,
/// which keeps expected 4xx paths from polluting the error rate.
/// </summary>
public sealed class RequestLoggingBehavior<TMessage, TResponse>(
    ILogger<RequestLoggingBehavior<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var messageType = typeof(TMessage).Name;
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var response = await next(message, cancellationToken);
            var elapsed = ElapsedMilliseconds(startedAt);

            logger.MessageHandled(messageType, elapsed);

            return response;
        }
        catch (QuerioException exception)
        {
            var elapsed = ElapsedMilliseconds(startedAt);

            logger.MessageFailed(messageType, exception.ErrorCode, elapsed);

            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var elapsed = ElapsedMilliseconds(startedAt);

            logger.MessageFaulted(exception, messageType, elapsed);

            throw;
        }
    }

    private static long ElapsedMilliseconds(long startedAt) =>
        (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
}
