using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Querio.Api.Tests.Api;

[Collection(nameof(QuerioApiCollection))]
public sealed class TenantEndpointsTests(QuerioApiFixture fixture) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Creating_an_organization_makes_the_creator_its_owner()
    {
        using var client = await ProvisionedClientAsync("uid-founder", "founder@example.com");

        using var response = await client.PostAsJsonAsync(
            "/api/v1/tenants",
            new { Name = "Ada Corp" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var tenant = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        tenant.GetProperty("name").GetString().ShouldBe("Ada Corp");
        tenant.GetProperty("slug").GetString().ShouldBe("ada-corp");
        tenant.GetProperty("role").GetString().ShouldBe("Owner");
        tenant.GetProperty("memberCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task An_unverified_email_cannot_create_an_organization()
    {
        using var client = await ProvisionedClientAsync(
            "uid-unverified", "unverified@example.com", emailVerified: false);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/tenants",
            new { Name = "Sketchy Corp" },
            TestContext.Current.CancellationToken);

        // Invitations are matched by email, so an unproven address must not become the
        // authority for who joins an organization.
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errorCode").GetString().ShouldBe("user.email_not_verified");
    }

    [Fact]
    public async Task A_blank_name_is_rejected_with_field_level_errors()
    {
        using var client = await ProvisionedClientAsync("uid-blank", "blank@example.com");

        using var response = await client.PostAsJsonAsync(
            "/api/v1/tenants",
            new { Name = "   " },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errorCode").GetString().ShouldBe("request.validation_failed");
        problem.GetProperty("errors").GetProperty("Name")[0].GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task A_duplicate_name_still_creates_an_organization_with_a_distinct_slug()
    {
        using var first = await ProvisionedClientAsync("uid-dup-1", "one@example.com");
        using var second = await ProvisionedClientAsync("uid-dup-2", "two@example.com");

        using var a = await first.PostAsJsonAsync("/api/v1/tenants", new { Name = "Acme" }, TestContext.Current.CancellationToken);
        using var b = await second.PostAsJsonAsync("/api/v1/tenants", new { Name = "Acme" }, TestContext.Current.CancellationToken);

        b.StatusCode.ShouldBe(HttpStatusCode.Created);

        var firstTenant = await a.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var secondTenant = await b.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        // Nobody should ever be asked to invent a unique slug themselves.
        firstTenant.GetProperty("slug").GetString().ShouldBe("acme");
        secondTenant.GetProperty("slug").GetString().ShouldBe("acme-2");
    }

    [Fact]
    public async Task A_new_account_belongs_to_no_organizations()
    {
        using var client = fixture.CreateAuthenticatedClient("uid-empty", "empty@example.com");

        using var response = await client.PostAsync("/api/v1/me/bootstrap", null, TestContext.Current.CancellationToken);

        var profile = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        // The client uses this to route to create-or-join rather than an empty app.
        profile.GetProperty("organizations").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Organizations_are_listed_in_a_stable_order()
    {
        using var client = await ProvisionedClientAsync("uid-many", "many@example.com");

        foreach (var name in (string[])["First Org", "Second Org", "Third Org"])
        {
            using var created = await client.PostAsJsonAsync("/api/v1/tenants", new { Name = name }, TestContext.Current.CancellationToken);
            created.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var names = await ReadOrganizationNamesAsync(client);

        // Querio does not remember the last organization used, so the client must land on the
        // same one every sign-in. Oldest membership first gives that.
        names.ShouldBe(["First Org", "Second Org", "Third Org"]);

        // And it must not drift between calls.
        (await ReadOrganizationNamesAsync(client)).ShouldBe(names);
    }

    [Fact]
    public async Task An_owner_can_rename_their_organization_without_changing_its_slug()
    {
        using var client = await ProvisionedClientAsync("uid-rename", "rename@example.com");
        var tenantId = await CreateTenantAsync(client, "Before");

        using var response = await client.PatchAsJsonAsync(
            $"/api/v1/tenants/{tenantId}",
            new { Name = "After" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var tenant = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        tenant.GetProperty("name").GetString().ShouldBe("After");
        tenant.GetProperty("slug").GetString().ShouldBe("before");
    }

    [Fact]
    public async Task An_owner_can_delete_their_organization()
    {
        using var client = await ProvisionedClientAsync("uid-delete", "delete@example.com");
        var tenantId = await CreateTenantAsync(client, "Doomed");

        using var deleted = await client.DeleteAsync($"/api/v1/tenants/{tenantId}", TestContext.Current.CancellationToken);
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var afterwards = await client.GetAsync($"/api/v1/tenants/{tenantId}", TestContext.Current.CancellationToken);
        afterwards.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await ReadOrganizationNamesAsync(client)).ShouldBeEmpty();
    }

    private async Task<HttpClient> ProvisionedClientAsync(
        string firebaseUid,
        string email,
        bool emailVerified = true)
    {
        var client = fixture.CreateAuthenticatedClient(firebaseUid, email, emailVerified);

        using var bootstrap = await client.PostAsync("/api/v1/me/bootstrap", null, TestContext.Current.CancellationToken);
        bootstrap.StatusCode.ShouldBe(HttpStatusCode.OK);

        return client;
    }

    private static async Task<Guid> CreateTenantAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/tenants",
            new { Name = name },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var tenant = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return tenant.GetProperty("id").GetGuid();
    }

    private static async Task<string[]> ReadOrganizationNamesAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return profile.GetProperty("organizations")
            .EnumerateArray()
            .Select(organization => organization.GetProperty("name").GetString()!)
            .ToArray();
    }
}
