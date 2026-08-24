using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Documents;
using Querio.Infrastructure.Embeddings;

namespace Querio.Api.Tests.Embeddings;

/// <summary>
/// Workers AI, driven by a stub transport.
///
/// Weighted towards the failures that leave no trace: pooling silently set to the wrong method,
/// a passage silently truncated, an error reported inside a 200. All three produce vectors of
/// exactly the right shape, so nothing downstream can tell they are worse.
/// </summary>
public sealed class CloudflareEmbeddingServiceTests
{
    [Fact]
    public async Task Passages_are_sent_without_a_prefix_and_the_query_with_one()
    {
        var bodies = new List<string>();
        var service = Build(request =>
        {
            bodies.Add(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());

            return Ok([Vector(2f)]);
        });

        await service.EmbedDocumentsAsync(["parental leave is 26 weeks"], TestContext.Current.CancellationToken);
        await service.EmbedQueryAsync("how much leave", TestContext.Current.CancellationToken);

        // Asymmetric on one side only, unlike the local provider. This family instructs the
        // query and takes the passage as it is; adding a prefix to both would be as wrong as
        // adding it to neither.
        bodies[0].ShouldContain("parental leave is 26 weeks");
        bodies[0].ShouldNotContain("Represent this sentence");

        bodies[1].ShouldContain("Represent this sentence for searching relevant passages: how much leave");
    }

    [Fact]
    public async Task Pooling_is_stated_rather_than_left_to_the_provider()
    {
        string? body = null;
        var service = Build(request =>
        {
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

            return Ok([Vector(1f)]);
        });

        await service.EmbedDocumentsAsync(["anything"], TestContext.Current.CancellationToken);

        // The API defaults to mean pooling and this model family is trained with CLS. Left at
        // the default it returns 768 perfectly well-formed values that simply rank worse — the
        // kind of fault that reads as the model being mediocre rather than as a bug.
        body.ShouldNotBeNull();
        body.ShouldContain("\"pooling\":\"cls\"");
    }

    [Fact]
    public async Task An_input_the_model_would_truncate_is_refused()
    {
        var service = Build(_ => Ok([Vector(1f)]));

        // 512 tokens is roughly 2,048 characters. Over that the provider truncates and still
        // returns a valid vector, which then represents only the part that fitted — a passage
        // answering from half of itself, with nothing to report it.
        var oversized = new string('a', 4_000);

        var failure = await Should.ThrowAsync<InvalidOperationException>(
            async () => await service.EmbedDocumentsAsync([oversized], TestContext.Current.CancellationToken));

        failure.Message.ShouldContain("truncated silently");
        failure.Message.ShouldContain("512");
    }

    [Fact]
    public async Task A_failure_reported_inside_a_success_response_is_not_treated_as_success()
    {
        var service = Build(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"success":false,"result":null,"errors":[{"code":7003,"message":"Could not route to the model"}]}""",
                Encoding.UTF8,
                "application/json"),
        });

        // The REST envelope carries failure in the body with a 200 status, so checking the
        // status code alone would read a refusal as an empty result.
        var failure = await Should.ThrowAsync<HttpRequestException>(
            async () => await service.EmbedDocumentsAsync(["anything"], TestContext.Current.CancellationToken));

        failure.Message.ShouldContain("7003");
        failure.Message.ShouldContain("Could not route to the model");
    }

    [Fact]
    public async Task A_spent_allowance_pauses_rather_than_retrying()
    {
        var calls = 0;
        var service = Build(_ =>
        {
            calls++;

            return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("""{"success":false,"errors":[{"code":10000,"message":"rate limited"}]}""", Encoding.UTF8, "application/json"),
            };
        });

        var failure = await Should.ThrowAsync<EmbeddingQuotaException>(
            async () => await service.EmbedDocumentsAsync(["anything"], TestContext.Current.CancellationToken));

        failure.IsDailyLimit.ShouldBeTrue();

        // Not retried here. The caller pauses the queue, which is the thing that knows how to
        // keep the passages already done and say so in the interface.
        calls.ShouldBe(1);
    }

    [Fact]
    public async Task Vectors_are_normalised_and_checked_for_dimensionality()
    {
        var service = Build(_ => Ok([Vector(3f)]));

        var vectors = await service.EmbedDocumentsAsync(["anything"], TestContext.Current.CancellationToken);

        Math.Sqrt(vectors[0].Sum(value => (double)value * value)).ShouldBe(1.0, 0.0001);

        var short_ = Build(_ => Ok([[.. Enumerable.Repeat(1f, 384)]]));

        var failure = await Should.ThrowAsync<InvalidOperationException>(
            async () => await short_.EmbedDocumentsAsync(["anything"], TestContext.Current.CancellationToken));

        failure.Message.ShouldContain("384");
        failure.Message.ShouldContain("bge-base-en-v1.5@768");
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
    public async Task The_account_and_model_appear_in_the_path()
    {
        string? path = null;
        var service = Build(request =>
        {
            path = request.RequestUri!.ToString();

            return Ok([Vector(1f)]);
        });

        await service.EmbedDocumentsAsync(["anything"], TestContext.Current.CancellationToken);

        path.ShouldNotBeNull();
        path.ShouldContain("accounts/test-account/ai/run/@cf/baai/bge-base-en-v1.5");
    }

    private static CloudflareEmbeddingService Build(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var options = new CloudflareEmbeddingOptions
        {
            AccountId = "test-account",
            ApiToken = "test-token",
        };

        var client = new HttpClient(new StubHandler(respond))
        {
            BaseAddress = new Uri(options.BaseAddress),
        };

        return new CloudflareEmbeddingService(client, Options.Create(options));
    }

    private static float[] Vector(float value) =>
        [.. Enumerable.Repeat(value, DocumentChunk.EmbeddingDimensions)];

    private static HttpResponseMessage Ok(IReadOnlyList<IReadOnlyList<float>> data) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    success = true,
                    errors = Array.Empty<object>(),
                    result = new { shape = new[] { data.Count, data.Count > 0 ? data[0].Count : 0 }, data },
                }),
                Encoding.UTF8,
                "application/json"),
        };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
