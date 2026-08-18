using System.Collections.Concurrent;
using System.Text;
using Serilog.Core;
using Serilog.Events;

namespace Querio.Api.Tests.Api;

/// <summary>
/// Keeps everything the application logs, so tests can assert on what does — and does not —
/// reach the log. Registered as an ILogEventSink, which Serilog's ReadFrom.Services picks up
/// alongside the sinks configured in appsettings.
/// </summary>
public sealed class CapturingLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<string> entries = new();

    public IReadOnlyCollection<string> Entries => entries.ToArray();

    public void Emit(LogEvent logEvent)
    {
        var builder = new StringBuilder();

        builder.Append(logEvent.RenderMessage(formatProvider: null));

        // Properties and exceptions are part of what gets written to disk, so a secret
        // leaking through either of them has still leaked.
        foreach (var (name, value) in logEvent.Properties)
        {
            builder.Append(' ').Append(name).Append('=').Append(value.ToString());
        }

        if (logEvent.Exception is not null)
        {
            builder.Append(' ').Append(logEvent.Exception);
        }

        entries.Enqueue(builder.ToString());
    }

    public void Clear() => entries.Clear();
}
