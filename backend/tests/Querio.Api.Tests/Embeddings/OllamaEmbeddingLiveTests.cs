using Microsoft.Extensions.Options;
using Querio.Domain.Documents;
using Querio.Infrastructure.Embeddings;

namespace Querio.Api.Tests.Embeddings;

/// <summary>
/// The local provider against a real Ollama.
///
/// Skips itself when nothing is listening, so continuous integration stays green without a model
/// server and a contributor without the stack running is not blocked by it. What is left is
/// exactly what a stub cannot prove: that the request shape is one Ollama accepts, that this
/// model really returns the dimensionality the column requires, and that the vectors mean
/// something.
///
/// That last one matters more here than for the hosted provider. This model is trained with
/// asymmetric instruction prefixes, and getting them wrong produces vectors of the right shape
/// that simply rank badly — a fault with no symptom other than worse answers.
/// </summary>
public sealed class OllamaEmbeddingLiveTests
{
    [Fact]
    public async Task The_model_returns_the_dimensionality_the_column_requires()
    {
        var service = Build();

        if (!await IsRunningAsync(service))
        {
            Assert.Skip("Ollama is not reachable; skipping the live check. Start it with docker compose up -d.");

            return;
        }

        var vectors = await service.EmbedDocumentsAsync(
            ["Parental leave is 26 weeks at full pay.", "Dental cover is included for partners."],
            TestContext.Current.CancellationToken);

        vectors.Count.ShouldBe(2);

        // Capable of 768 is not the same as returned 768 — this model supports Matryoshka
        // truncation, so the dimensionality is a property of the response, not of the name.
        vectors.ShouldAllBe(vector => vector.Length == DocumentChunk.EmbeddingDimensions);

        foreach (var vector in vectors)
        {
            Math.Sqrt(vector.Sum(value => (double)value * value)).ShouldBe(1.0, 0.0001);
        }
    }

    [Fact]
    public async Task Related_text_lands_closer_together_than_unrelated_text()
    {
        var service = Build();

        if (!await IsRunningAsync(service))
        {
            Assert.Skip("Ollama is not reachable; skipping the live check. Start it with docker compose up -d.");

            return;
        }

        var passages = await service.EmbedDocumentsAsync(
            [
                "Parental leave is 26 weeks at full pay.",
                "New parents may take six months of paid time off.",
                "The office car park is closed for resurfacing.",
            ],
            TestContext.Current.CancellationToken);

        var query = await service.EmbedQueryAsync(
            "how much parental leave do I get",
            TestContext.Current.CancellationToken);

        var leave = Similarity(query, passages[0]);
        var paraphrase = Similarity(query, passages[1]);
        var unrelated = Similarity(query, passages[2]);

        // The point is the ordering, not the magnitudes. If the document and query prefixes were
        // swapped or omitted this would still return three plausible numbers in the wrong order,
        // which is the failure worth a live test.
        leave.ShouldBeGreaterThan(unrelated);
        paraphrase.ShouldBeGreaterThan(unrelated);
    }

    /// <summary>Unit vectors, so the dot product is the cosine.</summary>
    private static double Similarity(float[] left, float[] right)
    {
        double total = 0;

        for (var index = 0; index < left.Length; index++)
        {
            total += (double)left[index] * right[index];
        }

        return total;
    }

    private static async Task<bool> IsRunningAsync(OllamaEmbeddingService service)
    {
        try
        {
            await service.EmbedQueryAsync("probe", TestContext.Current.CancellationToken);

            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    private static OllamaEmbeddingService Build()
    {
        var options = new OllamaEmbeddingOptions();
        var client = new HttpClient
        {
            BaseAddress = new Uri(options.BaseAddress),

            // Short, unlike the application's. A test that is going to skip should decide that
            // in seconds rather than sit through a two-minute model load.
            Timeout = TimeSpan.FromSeconds(30),
        };

        return new OllamaEmbeddingService(client, Options.Create(options));
    }
}
