using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Querio.Api.Tests.Api;

/// <summary>
/// Builds organizations and members through the real HTTP surface, including the real
/// invitation flow. Seeding straight into the database would let a broken endpoint pass its
/// own tests.
/// </summary>
internal sealed class TenantScenario(QuerioApiFixture fixture)
{
    public async Task<HttpClient> SignInAsync(string firebaseUid, string email, bool emailVerified = true)
    {
        var client = fixture.CreateAuthenticatedClient(firebaseUid, email, emailVerified);

        using var bootstrap = await client.PostAsync("/api/v1/me/bootstrap", null, TestContext.Current.CancellationToken);
        bootstrap.StatusCode.ShouldBe(HttpStatusCode.OK);

        return client;
    }

    public async Task<(HttpClient Owner, Guid TenantId)> OrganizationAsync(
        string firebaseUid,
        string email,
        string name)
    {
        var owner = await SignInAsync(firebaseUid, email);

        using var response = await owner.PostAsJsonAsync(
            "/api/v1/tenants",
            new { Name = name },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var tenant = await ReadAsync(response);

        return (owner, tenant.GetProperty("id").GetGuid());
    }

    public static async Task<string> InviteAsync(
        HttpClient inviter,
        Guid tenantId,
        string email,
        string role)
    {
        using var response = await inviter.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/invitations",
            new { Email = email, Role = role },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var invitation = await ReadAsync(response);

        return invitation.GetProperty("token").GetString()!;
    }

    /// <summary>Signs someone in and walks them through the real invitation redemption.</summary>
    public async Task<HttpClient> JoinAsync(
        HttpClient inviter,
        Guid tenantId,
        string firebaseUid,
        string email,
        string role)
    {
        var token = await InviteAsync(inviter, tenantId, email, role);
        var joiner = await SignInAsync(firebaseUid, email);

        using var accepted = await joiner.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new { Token = token },
            TestContext.Current.CancellationToken);

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);

        return joiner;
    }

    public static Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

    public static async Task<Guid> UserIdAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken);
        var profile = await ReadAsync(response);

        return profile.GetProperty("id").GetGuid();
    }
}
