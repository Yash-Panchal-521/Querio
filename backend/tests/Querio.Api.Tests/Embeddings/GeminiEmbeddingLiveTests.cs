using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Querio.Domain.Documents;
using Querio.Infrastructure.Embeddings;

namespace Querio.Api.Tests.Embeddings;

/// <summary>
/// The only tests here that talk to the real provider.
///
/// They skip themselves when no key is configured, so continuous integration stays green
/// without one and nobody has to put a credential in a workflow file. The key is read from the
/// API project's user secrets — never from a command line, where it would land in shell
/// history, and never from the repository.
///
/// Everything a stub can prove is proven in <see cref="GeminiEmbeddingServiceTests"/>. What is
/// left is the part a stub cannot: that the request shape is one the provider accepts, and
/// that the vectors coming back mean something.
/// </summary>
public sealed class GeminiEmbeddingLiveTests
{
    [Fact]
    public async Task The_provider_accepts_our_request_and_honours_the_dimensionality_we_ask_for()
    {
        using var service = Build(out var skipped);

        if (skipped)
        {
            Assert.Skip("No Embeddings:Gemini:ApiKey configured; skipping the live check.");

            return;
        }

        var vectors = await service.EmbedDocumentsAsync(
            ["Parental leave is 26 weeks at full pay.", "Dental cover is included for partners."],
            TestContext.Current.CancellationToken);

        vectors.Count.ShouldBe(2);

        // The whole reason we bypass the OpenAI-compatibility layer: it documents that
        // unsupported parameters are silently ignored, and 3072 dimensions arriving where 768
        // is expected would fail at the column, long after the request looked successful.
        vectors.ShouldAllBe(vector => vector.Length == DocumentChunk.EmbeddingDimensions);

        foreach (var vector in vectors)
        {
            Math.Sqrt(vector.Sum(value => (double)value * value)).ShouldBe(1.0, 0.0001);
        }
    }

    [Fact]
    public async Task Related_text_lands_closer_together_than_unrelated_text()
    {
        using var service = Build(out var skipped);

        if (skipped)
        {
            Assert.Skip("No Embeddings:Gemini:ApiKey configured; skipping the live check.");

            return;
        }

        var documents = await service.EmbedDocumentsAsync(
            [
                "Parental leave is 26 weeks at full pay.",
                "The office kitchen is restocked on Mondays.",
            ],
            TestContext.Current.CancellationToken);

        var query = await service.EmbedQueryAsync(
            "How much parental leave do I get?",
            TestContext.Current.CancellationToken);

        // Vectors are unit length, so the dot product is the cosine similarity.
        var toLeave = Dot(query, documents[0]);
        var toKitchen = Dot(query, documents[1]);

        // This is the assertion that would catch the mistakes nothing else can see: a task type
        // sent as the wrong constant, normalisation applied to the wrong axis, or a batch whose
        // order does not match its inputs. Each of those returns perfectly valid vectors that
        // simply retrieve badly.
        toLeave.ShouldBeGreaterThan(toKitchen);
    }

    private static double Dot(float[] left, float[] right)
    {
        double sum = 0;

        for (var index = 0; index < left.Length; index++)
        {
            sum += (double)left[index] * right[index];
        }

        return sum;
    }

    private static GeminiEmbeddingService Build(out bool skipped)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(Program).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var apiKey = configuration[$"{GeminiEmbeddingOptions.SectionName}:{nameof(GeminiEmbeddingOptions.ApiKey)}"]
            ?? Environment.GetEnvironmentVariable("QUERIO_GEMINI_API_KEY");

        skipped = string.IsNullOrWhiteSpace(apiKey);

        var options = new GeminiEmbeddingOptions { ApiKey = apiKey ?? "absent" };
        var client = new HttpClient();

        GeminiEmbeddingClientFactory.Configure(client, options);

        return new GeminiEmbeddingService(
            client,
            Options.Create(options),
            NullLogger<GeminiEmbeddingService>.Instance,
            TimeProvider.System,
            new EmbeddingAllowance(Options.Create(options), TimeProvider.System));
    }
}
