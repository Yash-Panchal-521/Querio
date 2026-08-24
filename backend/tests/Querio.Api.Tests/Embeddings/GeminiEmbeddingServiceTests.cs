using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Documents;
using Querio.Infrastructure.Embeddings;

namespace Querio.Api.Tests.Embeddings;

/// <summary>
/// The behaviours that do not depend on the provider being reachable, driven by a stub
/// transport so they run in milliseconds and cost nothing from a metered daily allowance.
/// </summary>
public sealed class GeminiEmbeddingServiceTests
{
    [Fact]
    public async Task Returned_vectors_are_normalised_to_unit_length()
    {
        // Deliberately not unit length. gemini-embedding-001 only normalises at its native
        // 3072 dimensions and we ask for 768, so this is ours to do — and an unnormalised
        // vector stores perfectly well and simply retrieves badly, which nothing would report.
        using var service = Build(_ => BatchResponse([Repeat(3f, DocumentChunk.EmbeddingDimensions)]));

        var vectors = await service.EmbedDocumentsAsync(["anything"], TestContext.Current.CancellationToken);

        Magnitude(vectors[0]).ShouldBe(1.0, 0.0001);
        vectors[0].Length.ShouldBe(DocumentChunk.EmbeddingDimensions);
    }

    [Fact]
    public async Task A_short_response_is_refused_rather_than_mismatched()
    {
        // Chunk N is embedded by vector N. If the counts disagree the mapping is unknowable,
        // and storing them anyway would attach vectors to the wrong passages — invisibly,
        // because every row would still look perfectly valid.
        using var service = Build(_ => BatchResponse([Repeat(1f, DocumentChunk.EmbeddingDimensions)]));

        var failure = await Should.ThrowAsync<InvalidOperationException>(
            async () => await service.EmbedDocumentsAsync(["one", "two"], TestContext.Current.CancellationToken));

        failure.Message.ShouldContain("received 1");
    }

    [Fact]
    public async Task A_spent_daily_allowance_is_reported_as_such_rather_than_retried()
    {
        var calls = 0;

        using var service = Build(_ =>
        {
            calls++;

            return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(
                    """{"error":{"message":"Quota exceeded for quota metric 'Requests' PerDay"}}""",
                    Encoding.UTF8,
                    "application/json"),
            };
        });

        var failure = await Should.ThrowAsync<EmbeddingQuotaException>(
            async () => await service.EmbedDocumentsAsync(["anything"], TestContext.Current.CancellationToken));

        failure.IsDailyLimit.ShouldBeTrue();

        // Retrying would spend the remaining attempts discovering the same answer. The caller
        // pauses the queue instead, and says so in the interface.
        calls.ShouldBe(1);
    }

    [Fact]
    public async Task Throttling_is_distinguished_from_a_spent_day()
    {
        using var service = Build(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent(
                """{"error":{"message":"Quota exceeded for quota metric 'Requests' PerMinute"}}""",
                Encoding.UTF8,
                "application/json"),
        });

        var failure = await Should.ThrowAsync<EmbeddingQuotaException>(
            async () => await service.EmbedDocumentsAsync(["anything"], TestContext.Current.CancellationToken));

        // A minute is worth waiting out; a day is not. The two get different pauses.
        failure.IsDailyLimit.ShouldBeFalse();
    }

    [Fact]
    public async Task A_stated_retry_delay_outranks_a_per_day_metric()
    {
        // Verbatim from a real refusal, which is the point: the metric names a per-day quota
        // *and* asks for a fifteen-second wait. Reading only the metric parked a document until
        // midnight UTC over something that had already cleared.
        using var service = Build(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent(
                """
                {"error":{"code":429,"message":"You exceeded your current quota.\n* Quota exceeded for metric: generativelanguage.googleapis.com/embed_content_free_tier_requests, limit: 1000, model: gemini-embedding-1.0\nPlease retry in 14.977703943s.","status":"RESOURCE_EXHAUSTED","details":[{"@type":"type.googleapis.com/google.rpc.RetryInfo","retryDelay":"14s"}]}}
                """,
                Encoding.UTF8,
                "application/json"),
        });

        var failure = await Should.ThrowAsync<EmbeddingQuotaException>(
            async () => await service.EmbedDocumentsAsync(["anything"], TestContext.Current.CancellationToken));

        failure.IsDailyLimit.ShouldBeFalse();

        // Taken from the response rather than guessed. The provider sends no Retry-After header,
        // so reading only headers threw away the one authoritative number it did send.
        failure.RetryAfter.ShouldNotBeNull();
        failure.RetryAfter!.Value.ShouldBeInRange(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));
    }

    [Fact]
    public async Task A_per_day_metric_with_no_stated_delay_is_still_a_spent_day()
    {
        using var service = Build(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent(
                """{"error":{"message":"Quota exceeded for quota metric 'Requests' PerDay"}}""",
                Encoding.UTF8,
                "application/json"),
        });

        var failure = await Should.ThrowAsync<EmbeddingQuotaException>(
            async () => await service.EmbedDocumentsAsync(["anything"], TestContext.Current.CancellationToken));

        // Nothing to go on but the metric, so the conservative reading stands.
        failure.IsDailyLimit.ShouldBeTrue();
        failure.RetryAfter.ShouldBeNull();
    }

    [Fact]
    public async Task A_long_stated_delay_on_a_per_day_metric_is_treated_as_a_spent_day()
    {
        using var service = Build(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent(
                """{"error":{"message":"Quota exceeded for quota metric 'Requests' PerDay. Please retry in 3600s."}}""",
                Encoding.UTF8,
                "application/json"),
        });

        var failure = await Should.ThrowAsync<EmbeddingQuotaException>(
            async () => await service.EmbedDocumentsAsync(["anything"], TestContext.Current.CancellationToken));

        // An hour is not "shortly". A stated delay only overrides the metric when it is short
        // enough that the queue is briefly slowed rather than parked.
        failure.IsDailyLimit.ShouldBeTrue();
    }

    [Fact]
    public async Task A_transient_failure_is_retried()
    {
        var calls = 0;

        using var service = Build(_ =>
        {
            calls++;

            return calls < 3
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("busy") }
                : BatchResponse([Repeat(1f, DocumentChunk.EmbeddingDimensions)]);
        });

        var vectors = await service.EmbedDocumentsAsync(["anything"], TestContext.Current.CancellationToken);

        vectors.Count.ShouldBe(1);
        calls.ShouldBe(3);
    }

    [Fact]
    public async Task More_passages_than_a_batch_holds_is_a_programming_error_not_a_request()
    {
        using var service = Build(_ => BatchResponse([]));

        // Caught before the request rather than after: an oversized batch would fail at the
        // provider and cost one of the day's allowance to learn nothing.
        await Should.ThrowAsync<ArgumentException>(
            async () => await service.EmbedDocumentsAsync(
                [.. Enumerable.Repeat("passage", 100)],
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_day_is_parked_before_a_request_leaves()
    {
        var calls = 0;
        var options = new GeminiEmbeddingOptions { ApiKey = "test-key", BatchSize = 32, PassagesPerDay = 3 };
        var allowance = new EmbeddingAllowance(Options.Create(options), TimeProvider.System);

        using var service = Build(
            _ =>
            {
                calls++;

                return BatchResponse([Repeat(1f, DocumentChunk.EmbeddingDimensions)]);
            },
            options,
            allowance);

        await service.EmbedDocumentsAsync(["one"], TestContext.Current.CancellationToken);
        await service.EmbedDocumentsAsync(["two"], TestContext.Current.CancellationToken);
        await service.EmbedDocumentsAsync(["three"], TestContext.Current.CancellationToken);

        calls.ShouldBe(3);

        var failure = await Should.ThrowAsync<EmbeddingQuotaException>(
            async () => await service.EmbedDocumentsAsync(["four"], TestContext.Current.CancellationToken));

        failure.IsDailyLimit.ShouldBeTrue();

        // Nothing went out. This is the whole point of counting on our side: a refusal costs the
        // provider's allowance exactly what a success costs, so a worker that learns the ceiling
        // by being told no spends tomorrow's allowance discovering that today's was gone.
        calls.ShouldBe(3);

        failure.RetryAfter.ShouldNotBeNull();
        failure.RetryAfter!.Value.ShouldBeGreaterThan(TimeSpan.Zero);
        failure.RetryAfter!.Value.ShouldBeLessThanOrEqualTo(TimeSpan.FromDays(1));
    }

    [Fact]
    public async Task The_allowance_is_shared_between_service_instances()
    {
        var calls = 0;
        var options = new GeminiEmbeddingOptions { ApiKey = "test-key", BatchSize = 32, PassagesPerDay = 2 };
        var allowance = new EmbeddingAllowance(Options.Create(options), TimeProvider.System);

        HttpResponseMessage Respond(HttpRequestMessage _)
        {
            calls++;

            return BatchResponse([Repeat(1f, DocumentChunk.EmbeddingDimensions)]);
        }

        // Two instances, because that is what production does: the typed client is transient and
        // the ingestion worker resolves one per job. A budget owned by the service would reset
        // between documents and constrain nothing across the only boundary that matters.
        using var first = Build(Respond, options, allowance);
        using var second = Build(Respond, options, allowance);

        await first.EmbedDocumentsAsync(["one"], TestContext.Current.CancellationToken);
        await second.EmbedDocumentsAsync(["two"], TestContext.Current.CancellationToken);

        await Should.ThrowAsync<EmbeddingQuotaException>(
            async () => await second.EmbedDocumentsAsync(["three"], TestContext.Current.CancellationToken));

        calls.ShouldBe(2);
    }

    [Fact]
    public async Task A_refusal_still_spends_the_day()
    {
        var options = new GeminiEmbeddingOptions { ApiKey = "test-key", BatchSize = 32, PassagesPerDay = 10 };
        var allowance = new EmbeddingAllowance(Options.Create(options), TimeProvider.System);

        using var refusing = Build(
            _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(
                    """{"error":{"message":"Quota exceeded. Please retry in 5s."}}""",
                    Encoding.UTF8,
                    "application/json"),
            },
            options,
            allowance);

        // Five passages refused, then five more: the provider's ceiling is evidently below ours,
        // and counting refusals is what makes the queue converge on parking itself instead of
        // waking every few seconds to be told no again.
        await Should.ThrowAsync<EmbeddingQuotaException>(async () => await refusing.EmbedDocumentsAsync(
            [.. Enumerable.Repeat("passage", 5)],
            TestContext.Current.CancellationToken));

        await Should.ThrowAsync<EmbeddingQuotaException>(async () => await refusing.EmbedDocumentsAsync(
            [.. Enumerable.Repeat("passage", 5)],
            TestContext.Current.CancellationToken));

        var parked = await Should.ThrowAsync<EmbeddingQuotaException>(
            async () => await refusing.EmbedDocumentsAsync(["one more"], TestContext.Current.CancellationToken));

        parked.IsDailyLimit.ShouldBeTrue();
    }

    private static GeminiEmbeddingService Build(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        GeminiEmbeddingOptions options,
        EmbeddingAllowance allowance)
    {
        var client = new HttpClient(new StubHandler(respond));

        GeminiEmbeddingClientFactory.Configure(client, options);

        return new GeminiEmbeddingService(
            client,
            Options.Create(options),
            NullLogger<GeminiEmbeddingService>.Instance,
            TimeProvider.System,
            allowance);
    }

    private static GeminiEmbeddingService Build(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var options = new GeminiEmbeddingOptions { ApiKey = "test-key", BatchSize = 32, MaxAttempts = 4 };
        var client = new HttpClient(new StubHandler(respond));

        GeminiEmbeddingClientFactory.Configure(client, options);

        return new GeminiEmbeddingService(
            client,
            Options.Create(options),
            NullLogger<GeminiEmbeddingService>.Instance,
            // The real clock, deliberately. A fake one never advances by itself, so the
            // backoff between retries would wait for a tick that never comes — the retry test
            // hung until it was killed. One and a half seconds of genuine delay in a single
            // test is cheaper than the machinery to drive a fake clock correctly from inside
            // the transport.
            TimeProvider.System,
            new EmbeddingAllowance(Options.Create(options), TimeProvider.System));
    }

    private static HttpResponseMessage BatchResponse(IReadOnlyList<float[]> vectors) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { embeddings = vectors.Select(values => new { values }) }),
                Encoding.UTF8,
                "application/json"),
        };

    private static float[] Repeat(float value, int length) => [.. Enumerable.Repeat(value, length)];

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
