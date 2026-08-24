using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Querio.Application.Common.Abstractions;

namespace Querio.Infrastructure.Embeddings;

/// <summary>
/// Embeddings from Cloudflare Workers AI.
///
/// An open-weights model on someone else's compute, which is the only shape that fits: the API
/// runs on 0.1 CPU, and there is no free second container to host a model in. Metered in tokens
/// rather than per passage, which is what makes bulk ingestion viable — a per-request allowance
/// punishes a long document for being long, and a token allowance simply prices it.
/// </summary>
internal sealed partial class CloudflareEmbeddingService : IEmbeddingService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient client;
    private readonly CloudflareEmbeddingOptions options;

    public CloudflareEmbeddingService(HttpClient client, IOptions<CloudflareEmbeddingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.client = client;
        this.options = options.Value;
    }

    public int MaxBatchSize => options.BatchSize;

    public string ModelIdentity => options.ModelIdentity;

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

        // No prefix on passages: this family instructs the query side only.
        var vectors = await SendAsync([.. passages], cancellationToken);

        if (vectors.Count != passages.Count)
        {
            // Positional: passage N is embedded by vector N. A mismatched count makes the
            // mapping unknowable, and storing them anyway attaches vectors to the wrong text
            // invisibly, because every row still looks valid.
            throw new InvalidOperationException(
                $"Asked for {passages.Count} embeddings and received {vectors.Count}.");
        }

        return vectors;
    }

    public async Task<float[]> EmbedQueryAsync(string query, CancellationToken cancellationToken)
    {
        var vectors = await SendAsync([options.QueryPrefix + query], cancellationToken);

        if (vectors.Count != 1)
        {
            throw new InvalidOperationException($"Asked for one embedding and received {vectors.Count}.");
        }

        return vectors[0];
    }

    private async Task<IReadOnlyList<float[]>> SendAsync(
        string[] inputs,
        CancellationToken cancellationToken)
    {
        GuardInputLength(inputs);

        var path = $"accounts/{options.AccountId}/ai/run/{options.Model}";

        using var response = await client.PostAsJsonAsync(
            path,
            new RunRequest(inputs, options.Pooling),
            Json,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode is HttpStatusCode.TooManyRequests)
        {
            // Left to the caller, which knows how to pause a queue and say so in the interface.
            // Retrying here would spend attempts learning what the pause already handles.
            throw new EmbeddingQuotaException(
                "The embedding allowance is spent for now. Ingestion resumes when it resets.",
                response.Headers.RetryAfter?.Delta,
                isDailyLimit: true);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Workers AI returned {(int)response.StatusCode} for {options.Model}: {Summarise(body)}");
        }

        var envelope = JsonSerializer.Deserialize<RunResponse>(body, Json)
            ?? throw new InvalidOperationException("Workers AI returned an empty body.");

        // The REST envelope reports failure in the body with a 200, so the status code alone is
        // not enough to know the call worked.
        if (!envelope.Success || envelope.Result is null)
        {
            var reported = envelope.Errors is { Count: > 0 }
                ? string.Join("; ", envelope.Errors.Select(error => $"{error.Code}: {error.Message}"))
                : Summarise(body);

            throw new HttpRequestException($"Workers AI refused the request: {reported}");
        }

        return [.. envelope.Result.Data.Select(vector => EmbeddingVector.Normalise(vector, ModelIdentity))];
    }

    /// <summary>
    /// Refuses text the model would silently truncate.
    ///
    /// The failure this prevents has no symptom: an over-long passage still comes back as a
    /// valid 768-dimensional vector, representing only the portion that fitted. It would store,
    /// index and rank without complaint while answering from half a passage.
    /// </summary>
    private void GuardInputLength(string[] inputs)
    {
        // Four characters to the token, the same approximation the chunker displays with a "≈".
        var ceiling = options.MaxInputTokens * 4;

        foreach (var input in inputs)
        {
            if (input.Length > ceiling)
            {
                throw new InvalidOperationException(
                    $"{ModelIdentity} accepts about {options.MaxInputTokens} tokens and this input is "
                    + $"roughly {input.Length / 4}. It would be truncated silently. "
                    + "Reduce the chunking target for this provider.");
            }
        }
    }

    private static string Summarise(string body) =>
        body.Length <= 400 ? body : string.Concat(body.AsSpan(0, 400), "…");

    private sealed record RunRequest(
        [property: JsonPropertyName("text")] string[] Text,
        [property: JsonPropertyName("pooling")] string Pooling);

    private sealed record RunResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("result")] RunResult? Result,
        [property: JsonPropertyName("errors")] IReadOnlyList<RunError>? Errors);

    private sealed record RunResult(
        [property: JsonPropertyName("shape")] IReadOnlyList<int>? Shape,
        [property: JsonPropertyName("data")] IReadOnlyList<IReadOnlyList<float>> Data);

    private sealed record RunError(
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("message")] string? Message);
}
