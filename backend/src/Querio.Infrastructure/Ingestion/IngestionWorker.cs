using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Querio.Application.Common.Abstractions;
using Querio.Infrastructure.Persistence;

namespace Querio.Infrastructure.Ingestion;

/// <summary>
/// Claims queued work and hands it to <see cref="IngestionJobRunner"/>, one job at a time.
///
/// One at a time on purpose. The instance this runs on has half a gigabyte of memory and a
/// quarter of a CPU, and the metered resource downstream is counted in requests per day —
/// parallelism would spend the same allowance faster while making every document slower.
///
/// A caveat worth stating plainly rather than discovering: on a host that suspends when idle,
/// a sleeping instance runs no worker. Ingestion resumes when the instance next wakes, which
/// in practice is the moment somebody opens the page that polls for status.
/// </summary>
internal sealed partial class IngestionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<IngestionOptions> options,
    TimeProvider timeProvider,
    ILogger<IngestionWorker> logger) : BackgroundService
{
    private readonly IngestionOptions options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            LogDisabled(logger);

            return;
        }

        var owner = IngestionJobStore.OwnerName();
        var idleDelay = TimeSpan.FromSeconds(options.IdlePollSeconds);

        LogStarted(logger, owner);

        while (!stoppingToken.IsCancellationRequested)
        {
            bool worked;

            try
            {
                worked = await ClaimAndRunAsync(owner, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // The loop itself must survive. A failure here means the queue is unreachable,
                // not that a document is bad — and an instance whose worker has quit looks
                // perfectly healthy while silently ingesting nothing.
                LogLoopFailure(logger, exception);
                worked = false;
            }

            if (!worked)
            {
                await Task.Delay(idleDelay, timeProvider, stoppingToken);
            }
        }
    }

    /// <returns>True when a job was claimed, so the loop knows not to sleep.</returns>
    private async Task<bool> ClaimAndRunAsync(string owner, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var store = new IngestionJobStore(scope.ServiceProvider.GetRequiredService<QuerioDbContext>());

        var job = await store.ClaimAsync(
            owner,
            timeProvider.GetUtcNow(),
            TimeSpan.FromSeconds(options.LeaseSeconds),
            cancellationToken);

        if (job is null)
        {
            return false;
        }

        // Adopted from the job before anything reads tenant-owned data, so the worker sees
        // exactly one organization's rows through the same default-deny filters a request
        // does. Without it the filters would hide everything, a worker having no request.
        scope.ServiceProvider.GetRequiredService<ITenantScope>().Establish(job.TenantId);

        await scope.ServiceProvider.GetRequiredService<IngestionJobRunner>()
            .RunAsync(job, cancellationToken);

        return true;
    }

    [LoggerMessage(EventId = 4300, Level = LogLevel.Information, Message = "Ingestion worker started as {Owner}.")]
    private static partial void LogStarted(ILogger logger, string owner);

    [LoggerMessage(EventId = 4301, Level = LogLevel.Warning, Message = "Ingestion worker is disabled by configuration.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(EventId = 4302, Level = LogLevel.Error, Message = "The ingestion loop failed and will continue.")]
    private static partial void LogLoopFailure(ILogger logger, Exception exception);
}
