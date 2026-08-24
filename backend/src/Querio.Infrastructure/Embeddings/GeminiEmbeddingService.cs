using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Documents;

namespace Querio.Infrastructure.Embeddings;

/// <summary>
/// Embeddings from Gemini's own API.
///
/// Not the OpenAI-compatibility layer, which documents that parameters it does not support
/// "will be silently ignored" — and we depend on two it does not document: batching, and
/// output dimensionality. Silently receiving 3072 dimensions would fail against a
/// halfvec(768) column, and only at the moment of writing. See ADR 0004.
/// </summary>
internal sealed partial class GeminiEmbeddingService : IEmbeddingService, IDisposable
{
    private const string DocumentTask = "RETRIEVAL_DOCUMENT";
    private const string QueryTask = "RETRIEVAL_QUERY";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient client;
    private readonly GeminiEmbeddingOptions options;
    private readonly ILogger<GeminiEmbeddingService> logger;
    private readonly TimeProvider timeProvider;
    private readonly EmbeddingAllowance allowance;

    public GeminiEmbeddingService(
        HttpClient client,
        IOptions<GeminiEmbeddingOptions> options,
        ILogger<GeminiEmbeddingService> logger,
        TimeProvider timeProvider,
        EmbeddingAllowance allowance)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.client = client;
        this.options = options.Value;
        this.logger = logger;
        this.timeProvider = timeProvider;

        // Shared, not built here. A typed HttpClient is transient, so a limiter owned by this
        // class is a fresh one per resolution — and the worker resolves one per job, which made
        // every document start with an untouched minute's allowance.
        this.allowance = allowance;
    }

    public int MaxBatchSize => options.BatchSize;

    public string ModelIdentity => $"{options.Model}@{DocumentChunk.EmbeddingDimensions}";

    public async Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(
        IReadOnlyList<string> passages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(passages);

        if (passages.Count == 0)
        {
            return [];
        }

        if (passages.Count > options.BatchSize)
        {
            throw new ArgumentException(
                $"At most {options.BatchSize} passages per call; got {passages.Count}.",
                nameof(passages));
        }

        var request = new BatchEmbedRequest(
            [.. passages.Select(passage => new EmbedRequest(
                $"models/{options.Model}",
                new Content([new Part(passage)]),
                DocumentTask,
                DocumentChunk.EmbeddingDimensions))]);

        var response = await SendAsync<BatchEmbedRequest, BatchEmbedResponse>(
            $"v1beta/models/{options.Model}:batchEmbedContents",
            request,
            passages.Count,
            EstimateTokens(passages),
            cancellationToken);

        if (response.Embeddings.Count != passages.Count)
        {
            // Positional: chunk N is embedded by vector N. A mismatched count means the mapping
            // is unknowable, and storing them anyway would attach vectors to the wrong text —
            // which no later check would catch, because every row would still look valid.
            throw new InvalidOperationException(
                $"Asked for {passages.Count} embeddings and received {response.Embeddings.Count}.");
        }

        return [.. response.Embeddings.Select(embedding => EmbeddingVector.Normalise(embedding.Values, ModelIdentity))];
    }

    public async Task<float[]> EmbedQueryAsync(string query, CancellationToken cancellationToken)
    {
        var request = new SingleEmbedRequest(
            $"models/{options.Model}",
            new Content([new Part(query)]),
            QueryTask,
            DocumentChunk.EmbeddingDimensions);

        var response = await SendAsync<SingleEmbedRequest, SingleEmbedResponse>(
            $"v1beta/models/{options.Model}:embedContent",
            request,
            1,
            EstimateTokens([query]),
            cancellationToken);

        return EmbeddingVector.Normalise(response.Embedding.Values, ModelIdentity);
    }

    /// <summary>
    /// Roughly four characters to the token — the same approximation the chunker displays with
    /// a "≈". Exactness would need the model's vocabulary, and the point here is only to keep a
    /// minute's worth of requests under a ceiling that has headroom built into it anyway.
    /// </summary>
    private static int EstimateTokens(IEnumerable<string> inputs) =>
        inputs.Sum(input => (input.Length / 4) + 1);

    private async Task<TResponse> SendAsync<TRequest, TResponse>(
        string path,
        TRequest payload,
        int passages,
        int estimatedTokens,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            // Throws before anything is sent when the day is gone, which is the point: being
            // refused costs what succeeding costs.
            using var lease = await allowance.AcquireAsync(passages, estimatedTokens, cancellationToken);

            using var response = await client.PostAsJsonAsync(path, payload, Json, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TResponse>(Json, cancellationToken)
                    ?? throw new InvalidOperationException("The provider returned an empty body.");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode is HttpStatusCode.TooManyRequests)
            {
                var quota = QuotaExceeded(response, body);

                // Counted even though it failed. The provider's ceiling may be lower than ours,
                // and without this a queue that keeps waking to be refused never converges on
                // parking itself.
                allowance.RecordRefusal(passages);

                // Logged because the daily-versus-throttle call below is a heuristic over this
                // body. Without it there is nothing to check the heuristic against when it
                // gets the answer wrong, and the symptom — a queue paused for the wrong length
                // of time — looks identical either way.
                LogRefused(logger, quota.IsDailyLimit, estimatedTokens, Summarise(body));

                // Not retried here. A minute's allowance is worth waiting out, a day's is not,
                // and the caller is the one that knows how to pause a queue and say so in the
                // interface. Retrying inside this method would spend attempts learning that.
                throw quota;
            }

            if (!IsTransient(response.StatusCode) || attempt >= options.MaxAttempts)
            {
                throw new HttpRequestException(
                    $"Embedding request failed with {(int)response.StatusCode}: {Summarise(body)}");
            }

            var delay = BackoffFor(attempt);

            LogRetrying(logger, attempt, options.MaxAttempts, (int)response.StatusCode, delay.TotalSeconds);

            await Task.Delay(delay, timeProvider, cancellationToken);
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    /// <summary>Exponential, with jitter so parallel workers do not resynchronise on retry.</summary>
    private static TimeSpan BackoffFor(int attempt) =>
        TimeSpan.FromMilliseconds((500 * Math.Pow(2, attempt - 1)) + Random.Shared.Next(0, 250));

    /// <summary>
    /// Any allowance long enough that waiting it out is a different kind of event — worth
    /// telling the user the queue is parked rather than briefly slowed.
    /// </summary>
    private static readonly TimeSpan LongWait = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The two shapes the provider states its delay in: a `retryDelay` among the error details,
    /// and the same number in prose in the message. Matched without escapes so the patterns read
    /// the same in any editor, and anchored on the label rather than on the surrounding JSON,
    /// which is documented as neither shape and may change.
    /// </summary>
    private static readonly Regex[] RetryDelayPatterns =
    [
        new("retryDelay[^0-9]*([0-9.]+)s", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new("retry in[ ]+([0-9.]+)[ ]*s", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    private static EmbeddingQuotaException QuotaExceeded(HttpResponseMessage response, string body)
    {
        // What the provider asked for, in preference to anything inferred. It states the delay
        // in the body and does not send a Retry-After header, so reading only the header meant
        // discarding the one authoritative number in the response.
        var retryAfter = RetryDelayIn(body) ?? response.Headers.RetryAfter?.Delta;

        // The quota identifier alone is not enough to decide this, and treating it as though it
        // were cost thirteen hours: a refusal naming a per-day metric arrived with "please retry
        // in 15s", was read as a spent daily allowance, and parked the document until midnight
        // UTC over something that had cleared before anyone could look at it.
        //
        // So a stated delay wins. A per-day metric means the day's allowance only when the
        // provider is not simultaneously saying it will be free again shortly.
        var namesDailyQuota = body.Contains("PerDay", StringComparison.OrdinalIgnoreCase)
            || body.Contains("per day", StringComparison.OrdinalIgnoreCase);

        var isDaily = namesDailyQuota && (retryAfter is null || retryAfter > LongWait);

        return new EmbeddingQuotaException(
            isDaily
                ? "The daily embedding allowance is spent. Ingestion resumes when it resets."
                : "Embedding requests are being throttled. Ingestion will resume shortly.",
            retryAfter,
            isDaily);
    }

    private static TimeSpan? RetryDelayIn(string body)
    {
        foreach (var pattern in RetryDelayPatterns)
        {
            var match = pattern.Match(body);

            if (match.Success
                && double.TryParse(
                    match.Groups[1].ValueSpan,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var seconds)
                && seconds is > 0 and < 86_400)
            {
                return TimeSpan.FromSeconds(seconds);
            }
        }

        return null;
    }

    private static string Summarise(string body) =>
        body.Length <= 500 ? body : string.Concat(body.AsSpan(0, 500), "…");

    // The allowance is a singleton owned by the container, so there is nothing here to dispose.
    public void Dispose()
    {
    }

    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Warning,
        Message = "Embedding request refused (daily limit: {IsDailyLimit}) after an estimated {EstimatedTokens} tokens: {Body}")]
    private static partial void LogRefused(ILogger logger, bool isDailyLimit, int estimatedTokens, string body);

    [LoggerMessage(
        EventId = 4100,
        Level = LogLevel.Warning,
        Message = "Embedding attempt {Attempt} of {MaxAttempts} failed with {StatusCode}; retrying in {DelaySeconds:F1}s.")]
    private static partial void LogRetrying(
        ILogger logger,
        int attempt,
        int maxAttempts,
        int statusCode,
        double delaySeconds);

    // Wire shapes. Records rather than anonymous objects so the contract is one thing to read.
    private sealed record BatchEmbedRequest([property: JsonPropertyName("requests")] IReadOnlyList<EmbedRequest> Requests);

    private sealed record EmbedRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("content")] Content Content,
        [property: JsonPropertyName("taskType")] string TaskType,
        [property: JsonPropertyName("outputDimensionality")] int OutputDimensionality);

    private sealed record SingleEmbedRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("content")] Content Content,
        [property: JsonPropertyName("taskType")] string TaskType,
        [property: JsonPropertyName("outputDimensionality")] int OutputDimensionality);

    private sealed record Content([property: JsonPropertyName("parts")] IReadOnlyList<Part> Parts);

    private sealed record Part([property: JsonPropertyName("text")] string Text);

    private sealed record BatchEmbedResponse(
        [property: JsonPropertyName("embeddings")] IReadOnlyList<EmbeddingValues> Embeddings);

    private sealed record SingleEmbedResponse(
        [property: JsonPropertyName("embedding")] EmbeddingValues Embedding);

    private sealed record EmbeddingValues([property: JsonPropertyName("values")] IReadOnlyList<float> Values);
}
