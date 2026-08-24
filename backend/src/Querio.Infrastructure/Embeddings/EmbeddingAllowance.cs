using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using Querio.Application.Common.Abstractions;

namespace Querio.Infrastructure.Embeddings;

/// <summary>
/// What we are allowed to spend on embedding, held in one place for the whole process.
///
/// This has to be a singleton, and the reason is not tidiness. A typed <c>HttpClient</c> is
/// registered transient, so a limiter built inside the embedding service is rebuilt every time
/// the service is resolved — and the ingestion worker resolves one per job. Every document
/// therefore started with a fresh minute's allowance no matter how recently the last one
/// finished, which is a rate limiter that limits nothing across the only boundary that matters.
///
/// Three ceilings, because the provider enforces three and they bind at different times:
/// requests per minute, tokens per minute, and — the one that actually stopped us — a count of
/// embedded passages per day.
/// </summary>
internal sealed class EmbeddingAllowance : IDisposable
{
    private readonly GeminiEmbeddingOptions options;
    private readonly TimeProvider timeProvider;
    private readonly FixedWindowRateLimiter requestsPerMinute;
    private readonly FixedWindowRateLimiter tokensPerMinute;
    private readonly Lock dailyGate = new();

    private DateOnly day;
    private int passagesToday;

    public EmbeddingAllowance(IOptions<GeminiEmbeddingOptions> options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options.Value;
        this.timeProvider = timeProvider;

        day = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        requestsPerMinute = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = this.options.RequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });

        tokensPerMinute = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = this.options.TokensPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    }

    /// <summary>
    /// Waits until this call fits inside the per-minute ceilings, and refuses outright when the
    /// day's passages are gone.
    /// </summary>
    /// <exception cref="EmbeddingQuotaException">
    /// The day's allowance is spent. Thrown before the request is sent, deliberately: a refusal
    /// costs the same allowance as a success, so discovering the ceiling by being told no is the
    /// one way to guarantee the next day starts behind. This is also what stops the queue
    /// looping — a paused job that wakes, asks, and is refused locally spends nothing.
    /// </exception>
    public async Task<IDisposable> AcquireAsync(
        int passages,
        int estimatedTokens,
        CancellationToken cancellationToken)
    {
        SpendPassages(passages);

        // Clamped because a single request can be worth more than a whole window if the budget
        // is configured low, and asking for more permits than exist throws rather than waiting.
        var tokenPermits = Math.Clamp(estimatedTokens, 1, options.TokensPerMinute);

        var requestLease = await requestsPerMinute.AcquireAsync(1, cancellationToken);
        var tokenLease = await tokensPerMinute.AcquireAsync(tokenPermits, cancellationToken);

        return new Leases(requestLease, tokenLease);
    }

    /// <summary>
    /// Accounts for passages the provider refused anyway, so a ceiling lower than ours still
    /// converges on parking the queue rather than probing it.
    /// </summary>
    public void RecordRefusal(int passages) => SpendPassages(passages, refusalOnly: true);

    private void SpendPassages(int passages, bool refusalOnly = false)
    {
        lock (dailyGate)
        {
            var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

            if (today != day)
            {
                day = today;
                passagesToday = 0;
            }

            if (refusalOnly)
            {
                passagesToday += passages;

                return;
            }

            if (passagesToday + passages > options.PassagesPerDay)
            {
                throw new EmbeddingQuotaException(
                    "The daily embedding allowance is spent. Ingestion resumes when it resets.",
                    TimeUntilTomorrow(),
                    isDailyLimit: true);
            }

            passagesToday += passages;
        }
    }

    private TimeSpan TimeUntilTomorrow()
    {
        var now = timeProvider.GetUtcNow();

        // Midnight UTC is an assumption, not a documented boundary — the provider does not
        // publish when the day rolls over for embedding models. It is only ever used when the
        // provider has told us nothing, and being early costs one refused request to learn.
        return new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero) - now;
    }

    public void Dispose()
    {
        requestsPerMinute.Dispose();
        tokensPerMinute.Dispose();
    }

    private sealed class Leases(IDisposable request, IDisposable tokens) : IDisposable
    {
        public void Dispose()
        {
            request.Dispose();
            tokens.Dispose();
        }
    }
}
