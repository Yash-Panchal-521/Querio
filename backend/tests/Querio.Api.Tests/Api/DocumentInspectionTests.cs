using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Querio.Application.Common.Abstractions;
using Querio.Infrastructure.Ingestion;
using Querio.Infrastructure.Persistence;

namespace Querio.Api.Tests.Api;

/// <summary>
/// Seeing what ingestion produced: the passages, the original file, and what the organization
/// has used. This is the half of the feature that makes the pipeline inspectable rather than a
/// black box somebody has to trust.
/// </summary>
[Collection(nameof(QuerioApiCollection))]
public sealed class DocumentInspectionTests(QuerioApiFixture fixture) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task The_chunk_inspector_shows_passages_in_order_and_never_the_vectors()
    {
        var (client, tenantId, documentId) = await IngestedAsync("owner-chunks", "owner.chunks@example.com");

        using var response = await client.GetAsync(
            $"/api/v1/tenants/{tenantId}/documents/{documentId}/chunks?skip=0&take=50",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var page = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var chunks = page.GetProperty("chunks");

        chunks.GetArrayLength().ShouldBeGreaterThan(0);
        page.GetProperty("total").GetInt32().ShouldBe(chunks.GetArrayLength());

        var first = chunks[0];
        first.GetProperty("ordinal").GetInt32().ShouldBe(0);
        first.GetProperty("hasEmbedding").GetBoolean().ShouldBeTrue();
        first.GetProperty("text").GetString().ShouldNotBeNullOrWhiteSpace();

        // Three kilobytes per passage that no interface can do anything with. The flag answers
        // the only question anybody actually has of it.
        first.TryGetProperty("embedding", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task A_download_link_says_when_it_expires()
    {
        var (client, tenantId, documentId) = await IngestedAsync("owner-download", "owner.download@example.com");

        using var response = await client.PostAsync(
            $"/api/v1/tenants/{tenantId}/documents/{documentId}/download-link",
            content: null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var link = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        link.GetProperty("url").GetString().ShouldNotBeNullOrWhiteSpace();

        // Stated rather than left implicit, so the interface can say when it stops working
        // instead of letting somebody find out by clicking a dead link.
        link.GetProperty("expiresAt").GetDateTimeOffset().ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Usage_reports_what_is_used_and_what_the_limit_is()
    {
        var (client, tenantId, _) = await IngestedAsync("owner-usage", "owner.usage@example.com");

        using var response = await client.GetAsync(
            $"/api/v1/tenants/{tenantId}/usage",
            TestContext.Current.CancellationToken);

        var usage = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        usage.GetProperty("documentCount").GetInt32().ShouldBe(1);
        usage.GetProperty("readyDocumentCount").GetInt32().ShouldBe(1);
        usage.GetProperty("chunkCount").GetInt32().ShouldBeGreaterThan(0);
        usage.GetProperty("storedBytes").GetInt64().ShouldBeGreaterThan(0);

        // Both halves. A limit somebody only meets by hitting it is indistinguishable from a
        // bug, and these free tiers are finite enough that people will reach them.
        usage.GetProperty("maxDocuments").GetInt32().ShouldBeGreaterThan(0);
        usage.GetProperty("maxStoredBytes").GetInt64().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Another_organization_cannot_inspect_or_download_the_document()
    {
        var (_, tenantId, documentId) = await IngestedAsync("owner-private", "owner.private@example.com");

        var (outsider, _) = await new TenantScenario(fixture)
            .OrganizationAsync("outsider-inspect", "outsider.inspect@example.com", "Elsewhere");

        foreach (var path in new[]
        {
            $"/api/v1/tenants/{tenantId}/documents/{documentId}",
            $"/api/v1/tenants/{tenantId}/documents/{documentId}/chunks",
        })
        {
            using var refused = await outsider.GetAsync(path, TestContext.Current.CancellationToken);

            // 404 rather than 403 — a 403 would confirm the organization exists.
            refused.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        using var download = await outsider.PostAsync(
            $"/api/v1/tenants/{tenantId}/documents/{documentId}/download-link",
            content: null,
            TestContext.Current.CancellationToken);

        download.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_unreasonable_page_size_is_refused()
    {
        var (client, tenantId, documentId) = await IngestedAsync("owner-paging", "owner.paging@example.com");

        using var response = await client.GetAsync(
            $"/api/v1/tenants/{tenantId}/documents/{documentId}/chunks?take=5000",
            TestContext.Current.CancellationToken);

        // Without a ceiling, one request could ask for every passage in the database.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<(HttpClient Client, Guid TenantId, Guid DocumentId)> IngestedAsync(string uid, string email)
    {
        var (client, tenantId) = await new TenantScenario(fixture).OrganizationAsync(uid, email, "Contoso");

        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes(
            "# Handbook\n\n## Leave\n\nParental leave is 26 weeks at full pay."));

        file.Headers.ContentType = new MediaTypeHeaderValue("text/markdown");
        form.Add(file, "file", "handbook.md");

        using var uploaded = await client.PostAsync(
            $"/api/v1/tenants/{tenantId}/documents",
            form,
            TestContext.Current.CancellationToken);

        uploaded.EnsureSuccessStatusCode();

        var document = await uploaded.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var documentId = document.GetProperty("id").GetGuid();

        await RunQueuedJobAsync();

        return (client, tenantId, documentId);
    }

    private async Task RunQueuedJobAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var store = new IngestionJobStore(scope.ServiceProvider.GetRequiredService<QuerioDbContext>());

        var job = await store.ClaimAsync(
            "inspection-test",
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(2),
            TestContext.Current.CancellationToken);

        job.ShouldNotBeNull();

        scope.ServiceProvider.GetRequiredService<ITenantScope>().Establish(job.TenantId);

        await scope.ServiceProvider.GetRequiredService<IngestionJobRunner>()
            .RunAsync(job, TestContext.Current.CancellationToken);
    }
}
