namespace Querio.Application.Common.Abstractions;

/// <summary>
/// Turns text into vectors.
///
/// Batched by design rather than as an optimisation. The free allowance is counted in requests
/// per day, not tokens, so embedding one passage per request would cap the product at roughly
/// twenty documents a day — the batch is what makes the free tier usable at all.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// The most inputs one request may carry. Configured rather than assumed: the provider's
    /// documented limit is silent on this, so it is a setting to change rather than a constant
    /// to rediscover.
    /// </summary>
    int MaxBatchSize { get; }

    /// <summary>
    /// A stable identifier for the model and dimensionality behind this provider, stored beside
    /// every vector it produces — <c>nomic-embed-text-v1.5@768</c>.
    ///
    /// Recorded because vectors are only comparable to others from the same model. Development
    /// and production deliberately run different providers, so without this a database carries
    /// no way to tell whether a similarity search is meaningful.
    /// </summary>
    string ModelIdentity { get; }

    /// <summary>
    /// Embeds passages for storage.
    ///
    /// Separate from <see cref="EmbedQueryAsync"/> on purpose: retrieval is asymmetric, and the
    /// provider is told whether it is embedding something to be found or something doing the
    /// finding. Using one for the other quietly costs recall in a way no test would notice.
    /// </summary>
    /// <returns>One vector per input, in the same order.</returns>
    Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(
        IReadOnlyList<string> passages,
        CancellationToken cancellationToken);

    /// <summary>Embeds a question, for matching against stored passages.</summary>
    Task<float[]> EmbedQueryAsync(string query, CancellationToken cancellationToken);
}

/// <summary>
/// The provider refused because an allowance is spent.
///
/// Distinct from a transient failure because the response is different: a transient failure is
/// retried in seconds, and an exhausted daily allowance means stopping until
/// <see cref="RetryAfter"/> rather than burning the remaining attempts discovering the same
/// answer.
/// </summary>
public sealed class EmbeddingQuotaException(string message, TimeSpan? retryAfter, bool isDailyLimit)
    : Exception(message)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;

    /// <summary>True when the day's allowance is gone rather than the minute's.</summary>
    public bool IsDailyLimit { get; } = isDailyLimit;
}
