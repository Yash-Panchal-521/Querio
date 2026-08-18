using System.Net;
using System.Net.Http.Json;

namespace Querio.Api.Tests.Api;

[Collection(nameof(QuerioApiCollection))]
public sealed class InvitationEndpointsTests(QuerioApiFixture fixture) : IAsyncLifetime
{
    private readonly TenantScenario scenario = new(fixture);

    public async ValueTask InitializeAsync() => await fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task An_invited_teammate_can_join()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-o1", "o1@example.com", "Acme");

        var joiner = await scenario.JoinAsync(owner, tenantId, "uid-j1", "joiner@example.com", "Member");

        using var response = await joiner.GetAsync($"/api/v1/tenants/{tenantId}", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var tenant = await TenantScenario.ReadAsync(response);
        tenant.GetProperty("role").GetString().ShouldBe("Member");
        tenant.GetProperty("memberCount").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task The_token_is_returned_once_and_never_listed_again()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-o2", "o2@example.com", "Acme");

        var token = await TenantScenario.InviteAsync(owner, tenantId, "pending@example.com", "Member");
        token.ShouldNotBeNullOrWhiteSpace();

        using var listed = await owner.GetAsync(
            $"/api/v1/tenants/{tenantId}/invitations",
            TestContext.Current.CancellationToken);

        // Only a hash is stored, so nothing can hand the token back — including us.
        var body = await listed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldNotContain(token);
        body.ShouldContain("pending@example.com");
    }

    [Fact]
    public async Task A_forwarded_link_is_useless_to_a_different_address()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-o3", "o3@example.com", "Acme");
        var token = await TenantScenario.InviteAsync(owner, tenantId, "intended@example.com", "Member");

        var wrongPerson = await scenario.SignInAsync("uid-wrong", "someone.else@example.com");

        using var response = await wrongPerson.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new { Token = token },
            TestContext.Current.CancellationToken);

        // The whole point of binding the invitation to an address.
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var stillOutside = await wrongPerson.GetAsync(
            $"/api/v1/tenants/{tenantId}",
            TestContext.Current.CancellationToken);

        stillOutside.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_link_cannot_be_redeemed_twice()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-o4", "o4@example.com", "Acme");
        var token = await TenantScenario.InviteAsync(owner, tenantId, "once@example.com", "Member");

        var joiner = await scenario.SignInAsync("uid-once", "once@example.com");

        using var first = await joiner.PostAsJsonAsync("/api/v1/invitations/accept", new { Token = token }, TestContext.Current.CancellationToken);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Re-opening the link must land them in the organization, not show an error about
        // something that already worked.
        using var second = await joiner.PostAsJsonAsync("/api/v1/invitations/accept", new { Token = token }, TestContext.Current.CancellationToken);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);

        // But a different person cannot reuse it.
        var stranger = await scenario.SignInAsync("uid-stranger", "once@example.com");

        using var reuse = await stranger.PostAsJsonAsync("/api/v1/invitations/accept", new { Token = token }, TestContext.Current.CancellationToken);
        reuse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var problem = await TenantScenario.ReadAsync(reuse);
        problem.GetProperty("errorCode").GetString().ShouldBe("invitation.already_accepted");
    }

    [Fact]
    public async Task A_revoked_link_stops_working_at_once()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-o5", "o5@example.com", "Acme");
        var token = await TenantScenario.InviteAsync(owner, tenantId, "revoked@example.com", "Member");

        using var listed = await owner.GetAsync($"/api/v1/tenants/{tenantId}/invitations", TestContext.Current.CancellationToken);
        var invitations = await TenantScenario.ReadAsync(listed);
        var invitationId = invitations[0].GetProperty("id").GetGuid();

        using var revoked = await owner.DeleteAsync(
            $"/api/v1/tenants/{tenantId}/invitations/{invitationId}",
            TestContext.Current.CancellationToken);

        revoked.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var joiner = await scenario.SignInAsync("uid-revoked", "revoked@example.com");

        using var response = await joiner.PostAsJsonAsync("/api/v1/invitations/accept", new { Token = token }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var problem = await TenantScenario.ReadAsync(response);
        problem.GetProperty("errorCode").GetString().ShouldBe("invitation.revoked");
    }

    [Fact]
    public async Task Inviting_an_existing_member_is_refused()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-o6", "o6@example.com", "Acme");
        await scenario.JoinAsync(owner, tenantId, "uid-member6", "member6@example.com", "Member");

        using var response = await owner.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/invitations",
            new { Email = "member6@example.com", Role = "Member" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var problem = await TenantScenario.ReadAsync(response);
        problem.GetProperty("errorCode").GetString().ShouldBe("membership.already_exists");
    }

    [Fact]
    public async Task A_second_invitation_to_the_same_address_is_refused_while_one_is_open()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-o7", "o7@example.com", "Acme");

        await TenantScenario.InviteAsync(owner, tenantId, "dup@example.com", "Member");

        using var response = await owner.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/invitations",
            new { Email = "dup@example.com", Role = "Member" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var problem = await TenantScenario.ReadAsync(response);
        problem.GetProperty("errorCode").GetString().ShouldBe("invitation.already_pending");
    }

    [Fact]
    public async Task A_meaningless_token_reveals_nothing()
    {
        var stranger = await scenario.SignInAsync("uid-guess", "guess@example.com");

        using var response = await stranger.PostAsJsonAsync(
            "/api/v1/invitations/preview",
            new { Token = "definitely-not-a-real-token" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_preview_tells_the_invitee_which_organization_and_address()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-o8", "o8@example.com", "Preview Corp");
        var token = await TenantScenario.InviteAsync(owner, tenantId, "previewee@example.com", "Admin");

        var invitee = await scenario.SignInAsync("uid-preview", "previewee@example.com");

        using var response = await invitee.PostAsJsonAsync(
            "/api/v1/invitations/preview",
            new { Token = token },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var preview = await TenantScenario.ReadAsync(response);
        preview.GetProperty("organizationName").GetString().ShouldBe("Preview Corp");
        preview.GetProperty("email").GetString().ShouldBe("previewee@example.com");
        preview.GetProperty("role").GetString().ShouldBe("Admin");
    }

    [Fact]
    public async Task Invitations_from_another_organization_are_invisible()
    {
        var (ownerA, tenantA) = await scenario.OrganizationAsync("uid-a", "a@example.com", "Alpha");
        var (ownerB, tenantB) = await scenario.OrganizationAsync("uid-b", "b@example.com", "Beta");

        await TenantScenario.InviteAsync(ownerA, tenantA, "target@example.com", "Member");

        using var listed = await ownerB.GetAsync($"/api/v1/tenants/{tenantB}/invitations", TestContext.Current.CancellationToken);
        var invitations = await TenantScenario.ReadAsync(listed);

        // The data-layer filter, not a WHERE clause anyone had to remember.
        invitations.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task An_admin_cannot_invite_someone_as_an_owner()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-o9", "o9@example.com", "Acme");
        var admin = await scenario.JoinAsync(owner, tenantId, "uid-admin9", "admin9@example.com", "Admin");

        using var response = await admin.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/invitations",
            new { Email = "newowner@example.com", Role = "Owner" },
            TestContext.Current.CancellationToken);

        // Otherwise an admin could promote themselves through a second account.
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_plain_member_cannot_invite_anyone()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-o10", "o10@example.com", "Acme");
        var member = await scenario.JoinAsync(owner, tenantId, "uid-m10", "m10@example.com", "Member");

        using var response = await member.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/invitations",
            new { Email = "someone@example.com", Role = "Member" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
