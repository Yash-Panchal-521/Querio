using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Querio.Domain.Documents;
using Querio.Infrastructure.Embeddings;

namespace Querio.Api.Tests.Embeddings;

/// <summary>
/// The local provider, driven by a stub transport.
///
/// The invariants tested here are the ones that fail silently when they fail: a wrong number of
/// dimensions, an unnormalised vector, a passage embedded as though it were a question. None of
/// those throw on their own, and all three produce a database that stores perfectly and
/// retrieves badly.
/// </summary>
public sealed class OllamaEmbeddingServiceTests
{
    [Fact]
    public async Task Passages_are_sent_with_the_document_prefix()
    {
        string? body = null;

        var service = Build(
            request =>
            {
                body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

                return Ok([Vector(2f), Vector(3f)]);
            });

        await service.EmbedDocumentsAsync(["first", "second"], TestContext.Current.CancellationToken);

        // Asymmetric by training, not by convention. Embedding a passage without its prefix
        // costs recall without changing the shape of anything, so nothing downstream notices.
        body.ShouldNotBeNull();
        body.ShouldContain("search_document: first");
        body.ShouldContain("search_document: second");
        body.ShouldNotContain("search_query:");
    }

    [Fact]
    public async Task A_query_is_sent_with_the_query_prefix()
    {
        string? body = null;

        var service = Build(
            request =>
            {
                body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

                return Ok([Vector(1f)]);
            });

        await service.EmbedQueryAsync("how much leave do I get", TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.ShouldContain("search_query: how much leave do I get");
        body.ShouldNotContain("search_document:");
    }

    [Fact]
    public async Task A_batch_goes_out_as_one_request()
    {
        var requests = 0;

        var service = Build(
            _ =>
            {
                requests++;

                return Ok([.. Enumerable.Range(0, 5).Select(index => Vector(index + 1f))]);
            });

        var vectors = await service.EmbedDocumentsAsync(
            [.. Enumerable.Range(0, 5).Select(index => $"passage {index}")],
            TestContext.Current.CancellationToken);

        vectors.Count.ShouldBe(5);
        requests.ShouldBe(1);
    }

    [Fact]
    public async Task Vectors_are_normalised_to_unit_length()
    {
        // Deliberately not unit length. This model pools and returns raw values, so scaling is
        // ours to do — and an unnormalised vector stores fine and simply ranks badly.
        var service = Build(_ => Ok([Vector(4f)]));

        var vectors = await service.EmbedDocumentsAsync(["anything"], TestContext.Current.CancellationToken);

        Magnitude(vectors[0]).ShouldBe(1.0, 0.0001);
    }

    [Fact]
    public async Task A_wrong_number_of_dimensions_is_refused()
    {
        // The guard is provider-independent on purpose. A model *capable* of 768 dimensions is
        // not the same as a model that returned them — this one can emit fewer on request, and
        // a shorter vector reaching a fixed-width column fails at the database with an error
        // naming neither the provider nor the setting responsible.
        var service = Build(_ => Ok([[.. Enumerable.Repeat(1f, 512)]]));

        var failure = await Should.ThrowAsync<InvalidOperationException>(
            async () => await service.EmbedDocumentsAsync(["anything"], TestContext.Current.CancellationToken));

        failure.Message.ShouldContain("512");
        failure.Message.ShouldContain("nomic-embed-text-v1.5@768");
    }

    [Fact]
    public async Task A_short_response_is_refused_rather_than_mismatched()
    {
        var service = Build(_ => Ok([Vector(1f)]));

        var failure = await Should.ThrowAsync<InvalidOperationException>(
            async () => await service.EmbedDocumentsAsync(["one", "two"], TestContext.Current.CancellationToken));

        failure.Message.ShouldContain("received 1");
    }

    [Fact]
    public async Task A_stack_that_is_not_running_says_so()
    {
        var service = Build(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"error":"model 'nomic-embed-text' not found"}""", Encoding.UTF8, "application/json"),
        });

        var failure = await Should.ThrowAsync<HttpRequestException>(
            async () => await service.EmbedDocumentsAsync(["anything"], TestContext.Current.CancellationToken));

        // No retry and no pause: a local model does not throttle, so this is a stack that is not
        // up or a model never pulled. The message has to point at that rather than at a quota.
        failure.Message.ShouldContain("has the model been pulled");
    }

    [Fact]
    public void The_model_identity_carries_the_dimensionality()
    {
        var service = Build(_ => Ok([Vector(1f)]));

        // Both halves matter for compatibility: this model can emit fewer dimensions, so the
        // name alone does not identify the embedding space its vectors belong to.
        service.ModelIdentity.ShouldBe("nomic-embed-text-v1.5@768");
    }

    private static OllamaEmbeddingService Build(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var options = new OllamaEmbeddingOptions();
        var client = new HttpClient(new StubHandler(respond))
        {
            BaseAddress = new Uri(options.BaseAddress),
        };

        return new OllamaEmbeddingService(client, Options.Create(options));
    }

    private static float[] Vector(float value) =>
        [.. Enumerable.Repeat(value, DocumentChunk.EmbeddingDimensions)];

    private static HttpResponseMessage Ok(IReadOnlyList<IReadOnlyList<float>> embeddings) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { embeddings }),
                Encoding.UTF8,
                "application/json"),
        };

    private static double Magnitude(float[] vector) =>
        Math.Sqrt(vector.Sum(value => (double)value * value));

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
