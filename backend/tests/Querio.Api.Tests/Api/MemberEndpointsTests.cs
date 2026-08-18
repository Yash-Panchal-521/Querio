using System.Net;
using System.Net.Http.Json;

namespace Querio.Api.Tests.Api;

[Collection(nameof(QuerioApiCollection))]
public sealed class MemberEndpointsTests(QuerioApiFixture fixture) : IAsyncLifetime
{
    private readonly TenantScenario scenario = new(fixture);

    public async ValueTask InitializeAsync() => await fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Members_are_listed_with_owners_first()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-l1", "l1@example.com", "Acme");
        await scenario.JoinAsync(owner, tenantId, "uid-l2", "l2@example.com", "Member");
        await scenario.JoinAsync(owner, tenantId, "uid-l3", "l3@example.com", "Admin");

        using var response = await owner.GetAsync($"/api/v1/tenants/{tenantId}/members", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var members = await TenantScenario.ReadAsync(response);
        var roles = members.EnumerateArray().Select(member => member.GetProperty("role").GetString()).ToArray();

        // Reads as a hierarchy rather than in whatever order Postgres returned.
        roles.ShouldBe(["Owner", "Admin", "Member"]);
    }

    [Fact]
    public async Task An_owner_can_promote_and_demote()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-p1", "p1@example.com", "Acme");
        var member = await scenario.JoinAsync(owner, tenantId, "uid-p2", "p2@example.com", "Member");
        var memberId = await TenantScenario.UserIdAsync(member);

        using var promoted = await owner.PatchAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/members/{memberId}",
            new { Role = "Admin" },
            TestContext.Current.CancellationToken);

        promoted.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var check = await member.GetAsync($"/api/v1/tenants/{tenantId}", TestContext.Current.CancellationToken);
        var tenant = await TenantScenario.ReadAsync(check);
        tenant.GetProperty("role").GetString().ShouldBe("Admin");
    }

    [Fact]
    public async Task The_only_owner_cannot_demote_themselves()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-d1", "d1@example.com", "Acme");
        var ownerId = await TenantScenario.UserIdAsync(owner);

        using var response = await owner.PatchAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/members/{ownerId}",
            new { Role = "Member" },
            TestContext.Current.CancellationToken);

        // Otherwise the organization is left with nobody who can administer it.
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var problem = await TenantScenario.ReadAsync(response);
        problem.GetProperty("errorCode").GetString().ShouldBe("tenant.last_owner");
    }

    [Fact]
    public async Task The_only_owner_cannot_leave()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-d2", "d2@example.com", "Acme");

        using var response = await owner.DeleteAsync(
            $"/api/v1/tenants/{tenantId}/members/me",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task An_owner_can_leave_once_another_owner_exists()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-d3", "d3@example.com", "Acme");
        var successor = await scenario.JoinAsync(owner, tenantId, "uid-d4", "d4@example.com", "Member");
        var successorId = await TenantScenario.UserIdAsync(successor);

        using var promoted = await owner.PatchAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/members/{successorId}",
            new { Role = "Owner" },
            TestContext.Current.CancellationToken);

        promoted.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var left = await owner.DeleteAsync(
            $"/api/v1/tenants/{tenantId}/members/me",
            TestContext.Current.CancellationToken);

        left.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // And they lose access immediately afterwards.
        using var afterwards = await owner.GetAsync($"/api/v1/tenants/{tenantId}", TestContext.Current.CancellationToken);
        afterwards.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_member_removed_by_an_admin_loses_access()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-r1", "r1@example.com", "Acme");
        var admin = await scenario.JoinAsync(owner, tenantId, "uid-r2", "r2@example.com", "Admin");
        var member = await scenario.JoinAsync(owner, tenantId, "uid-r3", "r3@example.com", "Member");
        var memberId = await TenantScenario.UserIdAsync(member);

        using var removed = await admin.DeleteAsync(
            $"/api/v1/tenants/{tenantId}/members/{memberId}",
            TestContext.Current.CancellationToken);

        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var profile = await member.GetAsync("/api/v1/me", TestContext.Current.CancellationToken);
        var body = await TenantScenario.ReadAsync(profile);

        body.GetProperty("organizations").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task An_admin_cannot_remove_another_admin()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-x1", "x1@example.com", "Acme");
        var adminA = await scenario.JoinAsync(owner, tenantId, "uid-x2", "x2@example.com", "Admin");
        var adminB = await scenario.JoinAsync(owner, tenantId, "uid-x3", "x3@example.com", "Admin");
        var adminBId = await TenantScenario.UserIdAsync(adminB);

        using var response = await adminA.DeleteAsync(
            $"/api/v1/tenants/{tenantId}/members/{adminBId}",
            TestContext.Current.CancellationToken);

        // If they could, two admins could remove each other and the survivor would be
        // whoever clicked first.
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_admin_cannot_remove_the_owner()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-y1", "y1@example.com", "Acme");
        var admin = await scenario.JoinAsync(owner, tenantId, "uid-y2", "y2@example.com", "Admin");
        var ownerId = await TenantScenario.UserIdAsync(owner);

        using var response = await admin.DeleteAsync(
            $"/api/v1/tenants/{tenantId}/members/{ownerId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_admin_cannot_change_roles()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-z1", "z1@example.com", "Acme");
        var admin = await scenario.JoinAsync(owner, tenantId, "uid-z2", "z2@example.com", "Admin");
        var member = await scenario.JoinAsync(owner, tenantId, "uid-z3", "z3@example.com", "Member");
        var memberId = await TenantScenario.UserIdAsync(member);

        using var response = await admin.PatchAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/members/{memberId}",
            new { Role = "Admin" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_admin_may_step_down_by_removing_themselves()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-s1", "s1@example.com", "Acme");
        var admin = await scenario.JoinAsync(owner, tenantId, "uid-s2", "s2@example.com", "Admin");
        var adminId = await TenantScenario.UserIdAsync(admin);

        // Blocking this would mean an admin cannot leave without asking an owner.
        using var response = await admin.DeleteAsync(
            $"/api/v1/tenants/{tenantId}/members/{adminId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task An_outsider_cannot_list_members()
    {
        var (_, tenantId) = await scenario.OrganizationAsync("uid-out1", "out1@example.com", "Acme");
        var outsider = await scenario.SignInAsync("uid-out2", "out2@example.com");

        using var response = await outsider.GetAsync(
            $"/api/v1/tenants/{tenantId}/members",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
