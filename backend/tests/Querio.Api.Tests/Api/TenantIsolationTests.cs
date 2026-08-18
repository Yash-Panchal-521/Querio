using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Querio.Domain.Tenants;
using Querio.Infrastructure.Persistence;

namespace Querio.Api.Tests.Api;

[Collection(nameof(QuerioApiCollection))]
public sealed class TenantIsolationTests(QuerioApiFixture fixture) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Another_organization_reports_not_found_rather_than_forbidden()
    {
        var (_, insiderTenantId) = await OrganizationAsync("uid-insider", "insider@example.com", "Insider Corp");
        using var outsider = await ProvisionedClientAsync("uid-outsider", "outsider@example.com");

        using var response = await outsider.GetAsync(
            $"/api/v1/tenants/{insiderTenantId}",
            TestContext.Current.CancellationToken);

        // 403 would confirm the organization exists, letting anyone with an account discover
        // who Querio's customers are by probing identifiers.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errorCode").GetString().ShouldBe("resource.not_found");
    }

    [Fact]
    public async Task A_nonexistent_organization_is_indistinguishable_from_someone_elses()
    {
        var (_, realTenantId) = await OrganizationAsync("uid-real", "real@example.com", "Real Corp");
        using var outsider = await ProvisionedClientAsync("uid-prober", "prober@example.com");

        using var existing = await outsider.GetAsync($"/api/v1/tenants/{realTenantId}", TestContext.Current.CancellationToken);
        using var fictional = await outsider.GetAsync($"/api/v1/tenants/{Guid.CreateVersion7()}", TestContext.Current.CancellationToken);

        // If these differed, the difference itself would be the information leak.
        existing.StatusCode.ShouldBe(fictional.StatusCode);
        existing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_outsider_cannot_rename_or_delete_another_organization()
    {
        var (_, tenantId) = await OrganizationAsync("uid-owner-a", "ownera@example.com", "Target Corp");
        using var outsider = await ProvisionedClientAsync("uid-intruder", "intruder@example.com");

        using var rename = await outsider.PatchAsJsonAsync(
            $"/api/v1/tenants/{tenantId}",
            new { Name = "Hijacked" },
            TestContext.Current.CancellationToken);

        using var delete = await outsider.DeleteAsync(
            $"/api/v1/tenants/{tenantId}",
            TestContext.Current.CancellationToken);

        rename.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_member_may_read_the_organization_but_not_administer_it()
    {
        var (_, tenantId) = await OrganizationAsync("uid-boss", "boss@example.com", "Shared Corp");
        using var member = await JoinAsMemberAsync(tenantId, "uid-staff", "staff@example.com", TenantRole.Member);

        using var read = await member.GetAsync($"/api/v1/tenants/{tenantId}", TestContext.Current.CancellationToken);
        read.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var rename = await member.PatchAsJsonAsync(
            $"/api/v1/tenants/{tenantId}",
            new { Name = "Renamed By Member" },
            TestContext.Current.CancellationToken);

        // Forbidden, not not-found: they already know the organization exists, so concealing
        // it would be dishonest without protecting anything.
        rename.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_admin_still_cannot_rename_or_delete_the_organization()
    {
        var (_, tenantId) = await OrganizationAsync("uid-founder-b", "founderb@example.com", "Admin Corp");
        using var admin = await JoinAsMemberAsync(tenantId, "uid-admin", "admin@example.com", TenantRole.Admin);

        using var rename = await admin.PatchAsJsonAsync(
            $"/api/v1/tenants/{tenantId}",
            new { Name = "Admin Rename" },
            TestContext.Current.CancellationToken);

        using var delete = await admin.DeleteAsync($"/api/v1/tenants/{tenantId}", TestContext.Current.CancellationToken);

        rename.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        delete.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_members_role_is_reported_as_their_own_not_the_owners()
    {
        var (_, tenantId) = await OrganizationAsync("uid-owner-c", "ownerc@example.com", "Role Corp");
        using var member = await JoinAsMemberAsync(tenantId, "uid-member-c", "memberc@example.com", TenantRole.Member);

        using var response = await member.GetAsync($"/api/v1/tenants/{tenantId}", TestContext.Current.CancellationToken);

        var tenant = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        tenant.GetProperty("role").GetString().ShouldBe("Member");
    }

    [Fact]
    public async Task A_malformed_organization_id_does_not_reach_a_handler()
    {
        using var client = await ProvisionedClientAsync("uid-malformed", "malformed@example.com");

        using var response = await client.GetAsync("/api/v1/tenants/not-a-guid", TestContext.Current.CancellationToken);

        // The route constraint rejects it, so no handler ever sees an unparsable id.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<HttpClient> ProvisionedClientAsync(string firebaseUid, string email)
    {
        var client = fixture.CreateAuthenticatedClient(firebaseUid, email);

        using var bootstrap = await client.PostAsync("/api/v1/me/bootstrap", null, TestContext.Current.CancellationToken);
        bootstrap.StatusCode.ShouldBe(HttpStatusCode.OK);

        return client;
    }

    private async Task<(HttpClient Client, Guid TenantId)> OrganizationAsync(
        string firebaseUid,
        string email,
        string name)
    {
        var client = await ProvisionedClientAsync(firebaseUid, email);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/tenants",
            new { Name = name },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var tenant = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return (client, tenant.GetProperty("id").GetGuid());
    }

    /// <summary>
    /// Joins someone directly through the database. Invitations arrive in Epic 3; until then
    /// this is the only way to produce a non-owner member to test authorization against.
    /// </summary>
    private async Task<HttpClient> JoinAsMemberAsync(
        Guid tenantId,
        string firebaseUid,
        string email,
        TenantRole role)
    {
        var client = await ProvisionedClientAsync(firebaseUid, email);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

        var user = await dbContext.Users.SingleAsync(
            candidate => candidate.FirebaseUid == firebaseUid,
            TestContext.Current.CancellationToken);

        var tenant = await dbContext.Tenants
            .Include(candidate => candidate.Memberships)
            .SingleAsync(candidate => candidate.Id == tenantId, TestContext.Current.CancellationToken);

        tenant.AddMember(user.Id, role);

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return client;
    }
}
