using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Querio.Domain.Documents;
using Querio.Domain.Tenants;
using Querio.Infrastructure.Persistence;

namespace Querio.Api.Tests.Api;

[Collection(nameof(QuerioApiCollection))]
public sealed class DocumentEndpointsTests(QuerioApiFixture fixture) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Uploading_a_document_queues_it_for_ingestion()
    {
        var (owner, tenantId) = await OrganizationAsync("owner-upload", "owner.upload@example.com");

        using var response = await UploadAsync(owner, tenantId, "handbook.md", "# Leave\n\nParental leave is 26 weeks.");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var document = await ReadAsync(response);
        document.GetProperty("fileName").GetString().ShouldBe("handbook.md");
        document.GetProperty("format").GetString().ShouldBe(nameof(FileFormat.Markdown));
        document.GetProperty("status").GetString().ShouldBe(nameof(DocumentStatus.Pending));

        // The job is what makes the upload a promise rather than a file sitting in a bucket.
        // It is written in the same transaction as the document, so its absence would mean a
        // document that is never ingested and never explains why.
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

        var documentId = document.GetProperty("id").GetGuid();
        var job = await dbContext.IngestionJobs
            .SingleOrDefaultAsync(candidate => candidate.DocumentId == documentId, TestContext.Current.CancellationToken);

        job.ShouldNotBeNull();
        job.State.ShouldBe(IngestionJobState.Queued);
        job.Attempt.ShouldBe(0);

        fixture.Factory.DocumentStorage.Keys.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Uploading_the_same_file_again_returns_what_is_already_there()
    {
        var (owner, tenantId) = await OrganizationAsync("owner-dupe", "owner.dupe@example.com");

        using var first = await UploadAsync(owner, tenantId, "policy.txt", "Identical content.");
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        var firstId = (await ReadAsync(first)).GetProperty("id").GetGuid();

        // Same bytes under a different name — the hash is what decides, not the file name.
        using var second = await UploadAsync(owner, tenantId, "policy-copy.txt", "Identical content.");

        // 200, not 201: nothing was created. Embedding is the metered resource, and a second
        // copy would spend it to produce vectors identical to ones already stored.
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync(second)).GetProperty("id").GetGuid().ShouldBe(firstId);

        using var listed = await owner.GetAsync($"/api/v1/tenants/{tenantId}/documents", TestContext.Current.CancellationToken);
        var documents = await listed.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        documents.GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task A_file_we_cannot_read_is_refused_with_a_reason_a_person_can_act_on()
    {
        var (owner, tenantId) = await OrganizationAsync("owner-binary", "owner.binary@example.com");

        // A PNG header. Named .txt on purpose: the bytes decide, not the extension.
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };

        using var response = await UploadBytesAsync(owner, tenantId, "not-really-text.txt", png);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await ReadAsync(response);
        problem.GetProperty("errorCode").GetString().ShouldBe("request.validation_failed");
        var fileErrors = problem.GetProperty("errors").GetProperty("file")[0].GetString();
        fileErrors.ShouldNotBeNull();
        fileErrors.ShouldContain("not supported");

        // And in `detail`, which is what a client shows. The sentence was always in `errors`;
        // `detail` carried "One or more validation errors occurred." instead, so every refusal
        // in the interface read as a generic failure with the real reason one field away.
        problem.GetProperty("detail").GetString().ShouldBe(fileErrors);
    }

    [Fact]
    public async Task An_empty_file_is_refused()
    {
        var (owner, tenantId) = await OrganizationAsync("owner-empty", "owner.empty@example.com");

        using var response = await UploadBytesAsync(owner, tenantId, "nothing.txt", []);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var emptyProblem = await ReadAsync(response);
        var emptyError = emptyProblem.GetProperty("errors").GetProperty("file")[0].GetString();
        emptyError.ShouldNotBeNull();
        emptyError.ShouldContain("empty");
        emptyProblem.GetProperty("detail").GetString().ShouldBe(emptyError);
    }

    [Fact]
    public async Task Another_organization_can_neither_see_nor_delete_the_document()
    {
        var (owner, tenantId) = await OrganizationAsync("owner-isolated", "owner.isolated@example.com");
        using var uploaded = await UploadAsync(owner, tenantId, "secret.txt", "Confidential.");
        var documentId = (await ReadAsync(uploaded)).GetProperty("id").GetGuid();

        var (outsider, otherTenantId) = await OrganizationAsync("outsider", "outsider@example.com");

        using var listed = await outsider.GetAsync(
            $"/api/v1/tenants/{otherTenantId}/documents",
            TestContext.Current.CancellationToken);

        listed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await listed.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetArrayLength().ShouldBe(0);

        // Reaching for it by id across organizations is the attack the filter exists to stop.
        // 404 rather than 403 is deliberate — see TenantAwareAuthorizationResultHandler: a 403
        // would confirm the organization exists, letting anyone with an account discover
        // Querio's customers by probing identifiers.
        using var stolen = await outsider.DeleteAsync(
            $"/api/v1/tenants/{tenantId}/documents/{documentId}",
            TestContext.Current.CancellationToken);

        stolen.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deleting_a_document_removes_its_stored_file_too()
    {
        var (owner, tenantId) = await OrganizationAsync("owner-delete", "owner.delete@example.com");

        using var uploaded = await UploadAsync(owner, tenantId, "temporary.txt", "Delete me.");
        var documentId = (await ReadAsync(uploaded)).GetProperty("id").GetGuid();

        var storedKey = fixture.Factory.DocumentStorage.Keys.Single();

        using var deleted = await owner.DeleteAsync(
            $"/api/v1/tenants/{tenantId}/documents/{documentId}",
            TestContext.Current.CancellationToken);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Leaving the object behind would keep consuming the allowance the delete was meant to
        // reclaim, and nothing in the interface would ever mention it again.
        fixture.Factory.DocumentStorage.Contains(storedKey).ShouldBeFalse();
    }

    [Fact]
    public async Task A_member_cannot_delete_someone_elses_upload_but_an_admin_can()
    {
        var (owner, tenantId) = await OrganizationAsync("owner-perms", "owner.perms@example.com");

        var member = await new TenantScenario(fixture).JoinAsync(
            owner, tenantId, "member-perms", "member.perms@example.com", nameof(TenantRole.Member));

        using var uploaded = await UploadAsync(owner, tenantId, "owned-by-owner.txt", "Owner's file.");
        var documentId = (await ReadAsync(uploaded)).GetProperty("id").GetGuid();

        using var refused = await member.DeleteAsync(
            $"/api/v1/tenants/{tenantId}/documents/{documentId}",
            TestContext.Current.CancellationToken);

        // Both are members, so the endpoint policy cannot express this on its own — deleting
        // someone else's upload is an administrative act, and the handler is what says so.
        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var allowed = await owner.DeleteAsync(
            $"/api/v1/tenants/{tenantId}/documents/{documentId}",
            TestContext.Current.CancellationToken);

        allowed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task<(HttpClient Client, Guid TenantId)> OrganizationAsync(string uid, string email) =>
        await new TenantScenario(fixture).OrganizationAsync(uid, email, "Contoso");

    private static Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        Guid tenantId,
        string fileName,
        string content) =>
        UploadBytesAsync(client, tenantId, fileName, Encoding.UTF8.GetBytes(content));

    private static async Task<HttpResponseMessage> UploadBytesAsync(
        HttpClient client,
        Guid tenantId,
        string fileName,
        byte[] content)
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);

        return await client.PostAsync(
            $"/api/v1/tenants/{tenantId}/documents",
            form,
            TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
}
