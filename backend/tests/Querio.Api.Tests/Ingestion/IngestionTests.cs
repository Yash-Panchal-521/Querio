using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Querio.Api.Tests.Api;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Documents;
using Querio.Infrastructure.Ingestion;
using Querio.Infrastructure.Persistence;

namespace Querio.Api.Tests.Ingestion;

/// <summary>
/// The queue and the pipeline, driven directly rather than by waiting on the background loop.
///
/// Directly because the properties worth proving here — that two workers cannot claim the same
/// job, that an abandoned lease returns to the queue, that a retry does not duplicate — are
/// exactly the ones a timing-dependent test reports unreliably.
/// </summary>
[Collection(nameof(QuerioApiCollection))]
public sealed class IngestionTests(QuerioApiFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

    public async ValueTask InitializeAsync() => await fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task An_uploaded_document_becomes_searchable_passages()
    {
        var (client, tenantId) = await OrganizationAsync("owner-ingest", "owner.ingest@example.com");

        var documentId = await UploadAsync(client, tenantId, "handbook.md", """
            # Employee handbook

            ## Leave

            Parental leave is 26 weeks at full pay.

            ## Benefits

            Dental cover is included for partners.
            """);

        await RunNextJobAsync();

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = Untenanted(scope);

        var document = await dbContext.Documents.IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == documentId, TestContext.Current.CancellationToken);

        document.Status.ShouldBe(DocumentStatus.Ready);
        document.ChunkCount.ShouldBeGreaterThan(0);
        document.EmbeddedChunkCount.ShouldBe(document.ChunkCount);

        var chunks = await dbContext.DocumentChunks.IgnoreQueryFilters()
            .Where(chunk => chunk.DocumentId == documentId)
            .OrderBy(chunk => chunk.Ordinal)
            .ToListAsync(TestContext.Current.CancellationToken);

        chunks.Count.ShouldBe(document.ChunkCount);
        chunks.ShouldAllBe(chunk => chunk.Embedding != null);
        chunks.ShouldAllBe(chunk => chunk.Embedding!.Length == DocumentChunk.EmbeddingDimensions);

        // Structure survived the whole pipeline, which is what a citation will lean on.
        chunks.ShouldContain(chunk => chunk.Breadcrumb == "Employee handbook › Leave");
    }

    [Fact]
    public async Task Two_workers_never_claim_the_same_job()
    {
        var (client, tenantId) = await OrganizationAsync("owner-race", "owner.race@example.com");
        await UploadAsync(client, tenantId, "one.txt", "The only document in the queue.");

        // Both ask at the same moment. Without FOR UPDATE SKIP LOCKED both would read the same
        // queued row, both would believe they owned it, and the document would be embedded
        // twice — spending a metered allowance to produce identical vectors.
        var first = ClaimAsync("worker-a");
        var second = ClaimAsync("worker-b");

        var claimed = await Task.WhenAll(first, second);

        claimed.Count(job => job is not null).ShouldBe(1);
    }

    [Fact]
    public async Task An_abandoned_lease_returns_to_the_queue()
    {
        var (client, tenantId) = await OrganizationAsync("owner-lease", "owner.lease@example.com");
        await UploadAsync(client, tenantId, "abandoned.txt", "Claimed by a worker that then died.");

        var claimed = await ClaimAsync("worker-that-dies");
        claimed.ShouldNotBeNull();

        // Nothing sweeps for it and no operator intervenes. The next worker to ask simply finds
        // it eligible again, because the claim was a lease rather than a lock.
        var afterExpiry = await ClaimAsync("worker-that-survives", at: DateTimeOffset.UtcNow.AddHours(1));

        afterExpiry.ShouldNotBeNull();
        afterExpiry.Id.ShouldBe(claimed.Id);
        afterExpiry.Attempt.ShouldBe(2);
    }

    [Fact]
    public async Task Running_a_document_twice_does_not_duplicate_its_passages()
    {
        var (client, tenantId) = await OrganizationAsync("owner-rerun", "owner.rerun@example.com");
        var documentId = await UploadAsync(client, tenantId, "rerun.md", "# Title\n\nSome content worth embedding.");

        await RunNextJobAsync();

        var afterFirst = await ChunkCountAsync(documentId);
        afterFirst.ShouldBeGreaterThan(0);

        // A retry re-derives everything. Passages are cleared before they are rewritten, so the
        // second run replaces rather than appends — the unique index would catch it, but only
        // by failing the job.
        await RunPipelineDirectlyAsync(documentId, tenantId);

        (await ChunkCountAsync(documentId)).ShouldBe(afterFirst);
    }

    [Fact]
    public async Task A_file_with_no_readable_text_fails_with_a_reason_rather_than_retrying()
    {
        var (client, tenantId) = await OrganizationAsync("owner-empty", "owner.empty@example.com");

        // Valid UTF-8, accepted at upload, and nothing but whitespace once extracted.
        var documentId = await UploadAsync(client, tenantId, "blank.txt", "   \n\n   \n");

        await RunNextJobAsync();

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = Untenanted(scope);

        var document = await dbContext.Documents.IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == documentId, TestContext.Current.CancellationToken);

        document.Status.ShouldBe(DocumentStatus.Failed);
        document.FailureCode.ShouldBe(DocumentExtractionException.NoText);
        document.FailureReason.ShouldNotBeNull();

        var job = await dbContext.IngestionJobs
            .SingleAsync(candidate => candidate.DocumentId == documentId, TestContext.Current.CancellationToken);

        // Terminal, not retried. The file will not become readable on the fourth attempt, and
        // each attempt costs allowance that working documents need.
        job.State.ShouldBe(IngestionJobState.Failed);
    }

    [Fact]
    public async Task A_spent_allowance_pauses_the_document_rather_than_failing_it()
    {
        var (client, tenantId) = await OrganizationAsync("owner-quota", "owner.quota@example.com");
        var documentId = await UploadAsync(client, tenantId, "quota.txt", "Content that will hit the daily limit.");

        fixture.Factory.Embeddings.NextFailure =
            new EmbeddingQuotaException("Daily allowance spent.", retryAfter: null, isDailyLimit: true);

        await RunNextJobAsync();

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = Untenanted(scope);

        var document = await dbContext.Documents.IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == documentId, TestContext.Current.CancellationToken);

        // Nothing is wrong with this document and there is nothing for anyone to do. Calling it
        // failed would invite a re-upload that spends the allowance again when it returns.
        document.Status.ShouldBe(DocumentStatus.WaitingForQuota);

        var job = await dbContext.IngestionJobs
            .SingleAsync(candidate => candidate.DocumentId == documentId, TestContext.Current.CancellationToken);

        job.State.ShouldBe(IngestionJobState.Queued);

        // The attempt is given back: an exhausted allowance says nothing about whether this
        // document can be ingested, so spending a retry on it would eventually fail a perfectly
        // good file for a reason that was never its fault.
        job.Attempt.ShouldBe(0);
        job.AvailableAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);

        // Said in the interface, not only in the log. The two pauses need different sentences —
        // a throttle clears in a minute, a spent daily allowance does not — and the row can only
        // tell them apart if the document carries which one happened.
        document.PauseReason.ShouldBe("Daily allowance spent.");
        document.ResumesAt.ShouldBe(job.AvailableAt);
    }

    [Fact]
    public async Task A_pause_part_way_through_keeps_the_passages_already_embedded()
    {
        var (client, tenantId) = await OrganizationAsync("owner-resume", "owner.resume@example.com");

        // Comfortably more passages than one batch, so the run is interrupted with work done
        // and work left. The fake embeds eight at a time.
        var documentId = await UploadAsync(client, tenantId, "long.md", LongDocument());

        var embeddings = fixture.Factory.Embeddings;

        embeddings.FailAfterCalls = 2;
        embeddings.NextFailure = new EmbeddingQuotaException(
            "Embedding requests are being throttled.",
            retryAfter: TimeSpan.FromMinutes(1),
            isDailyLimit: false);

        await RunNextJobAsync();

        var afterPause = await DocumentAsync(documentId);

        // Precondition, asserted rather than assumed: the refusal is set for the third batch, so
        // a document that fits in two would sail past it and reach Ready, and the test would
        // then prove nothing while still passing.
        afterPause.ChunkCount.ShouldBeGreaterThan(embeddings.MaxBatchSize * 2);

        afterPause.Status.ShouldBe(DocumentStatus.WaitingForQuota);

        // The heart of it. Two batches went out before the refusal, so two batches' worth of
        // passages exist — and they must still exist. Starting over would re-spend the
        // allowance on passages that are already vectors, and a document needing more than one
        // minute's worth of tokens would then never finish at all.
        var embeddedSoFar = afterPause.EmbeddedChunkCount;

        embeddedSoFar.ShouldBe(embeddings.MaxBatchSize * 2);
        (await ChunkCountAsync(documentId)).ShouldBe(embeddedSoFar);

        var callsBeforeResume = embeddings.EmbedCallCount;

        // The queue offers it again once the wait is over.
        await MakeJobAvailableAsync(documentId);
        await RunNextJobAsync();

        var afterResume = await DocumentAsync(documentId);

        afterResume.Status.ShouldBe(DocumentStatus.Ready);
        afterResume.EmbeddedChunkCount.ShouldBe(afterResume.ChunkCount);
        (await ChunkCountAsync(documentId)).ShouldBe(afterResume.ChunkCount);

        // Forward progress, not repetition: the second run embedded only what was left. Without
        // this the count would include the first two batches a second time.
        var remaining = afterResume.ChunkCount - embeddedSoFar;
        var expectedCalls = (int)Math.Ceiling(remaining / (double)embeddings.MaxBatchSize);

        (embeddings.EmbedCallCount - callsBeforeResume).ShouldBe(expectedCalls);

        // And the pause is over, so nothing is left claiming it is waiting.
        afterResume.PauseReason.ShouldBeNull();
        afterResume.ResumesAt.ShouldBeNull();

        var ordinals = await OrdinalsAsync(documentId);

        ordinals.ShouldBe([.. Enumerable.Range(0, afterResume.ChunkCount)]);
    }

    [Fact]
    public async Task A_document_that_chunks_differently_is_embedded_from_the_start()
    {
        var (client, tenantId) = await OrganizationAsync("owner-rechunk", "owner.rechunk@example.com");
        var documentId = await UploadAsync(client, tenantId, "rechunk.md", LongDocument());

        await RunNextJobAsync();

        var ready = await DocumentAsync(documentId);

        ready.Status.ShouldBe(DocumentStatus.Ready);

        // Rewriting history: the counter says this document has more passages than it does, as
        // it would if chunking changed between releases. The prefix can no longer be trusted to
        // line up with the text, so the honest move is to redo it rather than resume into it.
        await SetChunkCountAsync(documentId, ready.ChunkCount + 5);

        var callsBefore = fixture.Factory.Embeddings.EmbedCallCount;

        await RunPipelineDirectlyAsync(documentId, tenantId);

        var again = await DocumentAsync(documentId);

        again.Status.ShouldBe(DocumentStatus.Ready);
        again.ChunkCount.ShouldBe(ready.ChunkCount);
        (await ChunkCountAsync(documentId)).ShouldBe(ready.ChunkCount);

        var expectedCalls = (int)Math.Ceiling(
            ready.ChunkCount / (double)fixture.Factory.Embeddings.MaxBatchSize);

        (fixture.Factory.Embeddings.EmbedCallCount - callsBefore).ShouldBe(expectedCalls);
    }

    [Fact]
    public async Task A_cleanup_job_removes_an_object_whose_document_is_already_gone()
    {
        var (client, tenantId) = await OrganizationAsync("owner-cleanup", "owner.cleanup@example.com");
        await UploadAsync(client, tenantId, "orphan.txt", "Its row will go before its bytes do.");

        var storageKey = fixture.Factory.DocumentStorage.Keys.Single();

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var dbContext = Untenanted(scope);

            dbContext.IngestionJobs.Add(
                IngestionJob.QueueObjectDeletion(tenantId, storageKey, DateTimeOffset.UtcNow.AddMinutes(-1)));

            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Two jobs are queued now — the upload's ingestion and this cleanup. Run until the
        // cleanup has been picked up.
        await RunNextJobAsync();
        await RunNextJobAsync();

        fixture.Factory.DocumentStorage.Contains(storageKey).ShouldBeFalse();
    }

    private async Task<IngestionJob?> ClaimAsync(string owner, DateTimeOffset? at = null)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var store = new IngestionJobStore(Untenanted(scope));

        return await store.ClaimAsync(
            owner,
            at ?? DateTimeOffset.UtcNow,
            Lease,
            TestContext.Current.CancellationToken);
    }

    /// <summary>Claims whatever is next and runs it, exactly as the worker loop would.</summary>
    private async Task RunNextJobAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = Untenanted(scope);
        var store = new IngestionJobStore(dbContext);

        var job = await store.ClaimAsync(
            "test-worker",
            DateTimeOffset.UtcNow,
            Lease,
            TestContext.Current.CancellationToken);

        job.ShouldNotBeNull("There was no queued job to run.");

        scope.ServiceProvider.GetRequiredService<ITenantScope>().Establish(job.TenantId);

        await scope.ServiceProvider.GetRequiredService<IngestionJobRunner>()
            .RunAsync(job, TestContext.Current.CancellationToken);
    }

    /// <summary>Re-runs the pipeline for a document without going through the queue.</summary>
    private async Task RunPipelineDirectlyAsync(Guid documentId, Guid tenantId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<ITenantScope>().Establish(tenantId);

        await scope.ServiceProvider.GetRequiredService<DocumentIngestionPipeline>()
            .RunAsync(documentId, () => Task.CompletedTask, TestContext.Current.CancellationToken);
    }

    /// <summary>Enough headings and body to chunk into several batches.</summary>
    private static string LongDocument()
    {
        // (char)10 rather than an escape so the shape of this file survives being edited by
        // tooling that treats backslashes as its own.
        var paragraphBreak = new string((char)10, 2);

        var sentence = string.Join(' ', Enumerable.Repeat("Sentences that carry enough text to matter.", 12));
        var builder = new StringBuilder("# Manual").Append(paragraphBreak);

        for (var section = 1; section <= 40; section++)
        {
            builder.Append("## Section ").Append(section).Append(paragraphBreak);

            for (var paragraph = 1; paragraph <= 4; paragraph++)
            {
                builder
                    .Append("Section ").Append(section)
                    .Append(" paragraph ").Append(paragraph).Append(". ")
                    .Append(sentence)
                    .Append(paragraphBreak);
            }
        }

        return builder.ToString();
    }

    private async Task<Document> DocumentAsync(Guid documentId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();

        return await Untenanted(scope).Documents.IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == documentId, TestContext.Current.CancellationToken);
    }

    private async Task<List<int>> OrdinalsAsync(Guid documentId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();

        return await Untenanted(scope).DocumentChunks.IgnoreQueryFilters()
            .Where(chunk => chunk.DocumentId == documentId)
            .OrderBy(chunk => chunk.Ordinal)
            .Select(chunk => chunk.Ordinal)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Brings a paused job's wait forward so the queue offers it again now.</summary>
    private async Task MakeJobAvailableAsync(Guid documentId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();

        await Untenanted(scope).IngestionJobs
            .Where(job => job.DocumentId == documentId)
            .ExecuteUpdateAsync(
                update => update.SetProperty(job => job.AvailableAt, DateTimeOffset.UtcNow.AddMinutes(-1)),
                TestContext.Current.CancellationToken);
    }

    private async Task SetChunkCountAsync(Guid documentId, int chunkCount)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();

        await Untenanted(scope).Documents.IgnoreQueryFilters()
            .Where(document => document.Id == documentId)
            .ExecuteUpdateAsync(
                update => update.SetProperty(document => document.ChunkCount, chunkCount),
                TestContext.Current.CancellationToken);
    }

    private async Task<int> ChunkCountAsync(Guid documentId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();

        return await Untenanted(scope).DocumentChunks.IgnoreQueryFilters()
            .CountAsync(chunk => chunk.DocumentId == documentId, TestContext.Current.CancellationToken);
    }

    private static QuerioDbContext Untenanted(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

    private async Task<(HttpClient Client, Guid TenantId)> OrganizationAsync(string uid, string email) =>
        await new TenantScenario(fixture).OrganizationAsync(uid, email, "Contoso");

    private static async Task<Guid> UploadAsync(HttpClient client, Guid tenantId, string fileName, string content)
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", fileName);

        using var response = await client.PostAsync(
            $"/api/v1/tenants/{tenantId}/documents",
            form,
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        var document = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return document.GetProperty("id").GetGuid();
    }
}
