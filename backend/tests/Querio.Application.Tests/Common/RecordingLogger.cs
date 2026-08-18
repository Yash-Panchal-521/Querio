using Microsoft.Extensions.Logging;

namespace Querio.Application.Tests.Common;

internal sealed record LogEntry(LogLevel Level, EventId EventId, string Message, Exception? Exception);

/// <summary>
/// Captures what was logged. Substituting ILogger directly is awkward because
/// source-generated logging calls the non-generic Log overload with an internal state type.
/// </summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> entries = [];

    public IReadOnlyList<LogEntry> Entries => entries;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception), exception));
    }
}
