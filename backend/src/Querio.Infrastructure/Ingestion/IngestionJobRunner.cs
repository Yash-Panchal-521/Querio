using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Documents;
using Querio.Infrastructure.Persistence;

namespace Querio.Infrastructure.Ingestion;

/// <summary>
/// Runs one claimed job and decides what its outcome means.
///
/// Separate from the worker so the decisions — which failures retry, which are terminal, which
/// are a pause rather than a failure — can be tested directly rather than by racing a
/// background loop and hoping.
/// </summary>
internal sealed partial class IngestionJobRunner(
    QuerioDbContext dbContext,
    DocumentIngestionPipeline pipeline,
    IDocumentStorage storage,
    IOptions<IngestionOptions> options,
    TimeProvider timeProvider,
    ILogger<IngestionJobRunner> logger)
{
    private readonly IngestionOptions options = options.Value;

    public async Task RunAsync(IngestionJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        try
        {
            switch (job.Kind)
            {
                case IngestionJobKind.IngestDocument:
                    await pipeline.RunAsync(
                        job.DocumentId!.Value,
                        () => RenewAsync(job, cancellationToken),
                        cancellationToken);

                    break;

                case IngestionJobKind.DeleteStoredObject:
                    await storage.DeleteAsync(job.StorageKey!, cancellationToken);
                    LogObjectCollected(logger, job.StorageKey!);

                    break;

                default:
                    throw new InvalidOperationException($"Unknown job kind {job.Kind}.");
            }

            job.Succeed();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down. The lease expires on its own and another worker picks the job up,
            // so there is nothing to unwind — which is the point of leasing rather than locking.
            throw;
        }
        catch (DocumentExtractionException extraction)
        {
            // Deterministic. Retrying a file we cannot read would spend four more attempts
            // reaching the same conclusion.
            await FailAsync(job, extraction.FailureCode, extraction.Message, cancellationToken);
        }
        catch (EmbeddingQuotaException quota)
        {
            await PauseAsync(job, quota, cancellationToken);
        }
        catch (Exception exception)
        {
            await RetryOrFailAsync(job, exception, cancellationToken);
        }
    }

    private async Task PauseAsync(IngestionJob job, EmbeddingQuotaException quota, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        // Until the allowance actually resets. A daily limit rolls over at midnight UTC; a
        // throttle clears in about a minute. Retrying sooner just spends the allowance being
        // refused again.
        var resumeAt = quota.IsDailyLimit
            ? new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero)
            : now.Add(quota.RetryAfter ?? TimeSpan.FromMinutes(1));

        job.PauseWithoutSpendingAnAttempt(resumeAt, quota.Message);

        if (job.DocumentId is { } documentId)
        {
            var document = await FindDocumentAsync(documentId, cancellationToken);

            // A state of its own, not a failure. Nothing is wrong with the document and there
            // is nothing for anyone to do — calling it failed would invite a pointless
            // re-upload that spends the allowance again when it returns.
            //
            // The reason and the resume time go onto the document, not just the job, because
            // the interface reads documents: it is what lets the row say which allowance ran
            // out, and decide whether this is worth watching or worth leaving alone.
            document?.WaitForQuota(quota.Message, resumeAt);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        LogPaused(logger, job.Id, quota.IsDailyLimit, quota.Message);
    }

    private async Task RetryOrFailAsync(IngestionJob job, Exception exception, CancellationToken cancellationToken)
    {
        if (!job.HasAttemptsLeft)
        {
            LogGaveUp(logger, job.Id, exception);

            await FailAsync(
                job,
                "ingestion.failed",
                "Something went wrong while processing this document. Try uploading it again.",
                cancellationToken);

            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Pow(3, job.Attempt));

        job.Reschedule(timeProvider.GetUtcNow().Add(delay), exception.Message);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogRetrying(logger, job.Id, job.Attempt, delay.TotalSeconds, exception);
    }

    private async Task FailAsync(
        IngestionJob job,
        string failureCode,
        string reason,
        CancellationToken cancellationToken)
    {
        job.FailPermanently(reason);

        if (job.DocumentId is { } documentId)
        {
            var document = await FindDocumentAsync(documentId, cancellationToken);

            // Recorded against the document rather than only in a log. The person who uploaded
            // it is the one who can act on it, and they are not reading the logs.
            document?.Fail(failureCode, reason);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task<Document?> FindDocumentAsync(Guid documentId, CancellationToken cancellationToken) =>
        dbContext.Documents.FirstOrDefaultAsync(candidate => candidate.Id == documentId, cancellationToken);

    private async Task RenewAsync(IngestionJob job, CancellationToken cancellationToken)
    {
        job.RenewLease(timeProvider.GetUtcNow().AddSeconds(options.LeaseSeconds));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(EventId = 4303, Level = LogLevel.Warning, Message = "Job {JobId} retried (attempt {Attempt}), next in {DelaySeconds:F0}s.")]
    private static partial void LogRetrying(ILogger logger, Guid jobId, int attempt, double delaySeconds, Exception exception);

    [LoggerMessage(EventId = 4304, Level = LogLevel.Error, Message = "Job {JobId} exhausted its attempts.")]
    private static partial void LogGaveUp(ILogger logger, Guid jobId, Exception exception);

    [LoggerMessage(EventId = 4305, Level = LogLevel.Warning, Message = "Job {JobId} paused (daily limit: {IsDailyLimit}): {Reason}")]
    private static partial void LogPaused(ILogger logger, Guid jobId, bool isDailyLimit, string reason);

    [LoggerMessage(EventId = 4306, Level = LogLevel.Information, Message = "Collected unreferenced object {StorageKey}.")]
    private static partial void LogObjectCollected(ILogger logger, string storageKey);
}
