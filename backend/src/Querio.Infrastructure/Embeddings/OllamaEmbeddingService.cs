using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Querio.Application.Common.Abstractions;

namespace Querio.Infrastructure.Embeddings;

/// <summary>
/// Embeddings from a locally-run Ollama, for development.
///
/// Not a stand-in for the hosted provider — the same interface, the same batching, the same
/// asymmetric document-versus-query treatment, the same dimension and normalisation checks. What
/// it removes is the allowance: nothing here is metered, so ingesting a large document twenty
/// times to test the pipeline costs nothing and blocks nobody.
///
/// Its vectors are not comparable with any other provider's, which is why every chunk records
/// the model that produced it. A development database and a production one hold different
/// embedding spaces by design.
/// </summary>
internal sealed partial class OllamaEmbeddingService : IEmbeddingService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient client;
    private readonly OllamaEmbeddingOptions options;

    public OllamaEmbeddingService(HttpClient client, IOptions<OllamaEmbeddingOptions> options)
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

        var inputs = passages.Select(passage => options.DocumentPrefix + passage).ToArray();
        var response = await SendAsync(inputs, cancellationToken);

        if (response.Embeddings.Count != passages.Count)
        {
            // Positional: passage N is embedded by vector N. A mismatched count makes the
            // mapping unknowable, and storing them anyway would attach vectors to the wrong
            // text — invisibly, because every row would still look valid.
            throw new InvalidOperationException(
                $"Asked for {passages.Count} embeddings and received {response.Embeddings.Count}.");
        }

        return [.. response.Embeddings.Select(embedding => EmbeddingVector.Normalise(embedding, ModelIdentity))];
    }

    public async Task<float[]> EmbedQueryAsync(string query, CancellationToken cancellationToken)
    {
        var response = await SendAsync([options.QueryPrefix + query], cancellationToken);

        if (response.Embeddings.Count != 1)
        {
            throw new InvalidOperationException(
                $"Asked for one embedding and received {response.Embeddings.Count}.");
        }

        return EmbeddingVector.Normalise(response.Embeddings[0], ModelIdentity);
    }

    private async Task<EmbedResponse> SendAsync(string[] inputs, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            "api/embed",
            new EmbedRequest(options.Model, inputs),
            Json,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            // No retry and no pause. A local model does not throttle, so a failure here is a
            // stack that is not running or a model that was never pulled — conditions a retry
            // cannot resolve and a developer fixes in one command.
            throw new HttpRequestException(
                $"Ollama returned {(int)response.StatusCode} for model '{options.Model}'. "
                + $"Is it running, and has the model been pulled? {Summarise(body)}");
        }

        return await response.Content.ReadFromJsonAsync<EmbedResponse>(Json, cancellationToken)
            ?? throw new InvalidOperationException("Ollama returned an empty body.");
    }

    private static string Summarise(string body) =>
        body.Length <= 300 ? body : string.Concat(body.AsSpan(0, 300), "…");

    private sealed record EmbedRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string[] Input);

    private sealed record EmbedResponse(
        [property: JsonPropertyName("embeddings")] IReadOnlyList<IReadOnlyList<float>> Embeddings);
}
