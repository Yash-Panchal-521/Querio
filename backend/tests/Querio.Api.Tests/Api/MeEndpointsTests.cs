using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Querio.Infrastructure.Persistence;

namespace Querio.Api.Tests.Api;

[Collection(nameof(QuerioApiCollection))]
public sealed class MeEndpointsTests(QuerioApiFixture fixture) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Bootstrap_provisions_a_profile_from_the_token()
    {
        using var client = fixture.CreateAuthenticatedClient(
            "uid-provision", "New.User@Example.com", emailVerified: true, "New User");

        using var response = await client.PostAsync("/api/v1/me/bootstrap", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        profile.GetProperty("email").GetString().ShouldBe("new.user@example.com");
        profile.GetProperty("displayName").GetString().ShouldBe("New User");
        profile.GetProperty("emailVerified").GetBoolean().ShouldBeTrue();
        profile.GetProperty("id").GetGuid().ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Bootstrap_is_idempotent_across_repeated_sign_ins()
    {
        using var client = fixture.CreateAuthenticatedClient("uid-repeat");

        using var first = await client.PostAsync("/api/v1/me/bootstrap", null, TestContext.Current.CancellationToken);
        using var second = await client.PostAsync("/api/v1/me/bootstrap", null, TestContext.Current.CancellationToken);

        var firstProfile = await first.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var secondProfile = await second.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        // Called on every sign-in, not just the first — a second call must not create a
        // second account or hand back a different id.
        secondProfile.GetProperty("id").GetGuid().ShouldBe(firstProfile.GetProperty("id").GetGuid());

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

        var count = await dbContext.Users.CountAsync(
            user => user.FirebaseUid == "uid-repeat",
            TestContext.Current.CancellationToken);

        count.ShouldBe(1);
    }

    [Fact]
    public async Task Bootstrap_picks_up_a_profile_changed_since_last_sign_in()
    {
        using var before = fixture.CreateAuthenticatedClient(
            "uid-changed", "old@example.com", emailVerified: false, "Old Name");

        using var _ = await before.PostAsync("/api/v1/me/bootstrap", null, TestContext.Current.CancellationToken);

        using var after = fixture.CreateAuthenticatedClient(
            "uid-changed", "new@example.com", emailVerified: true, "New Name");

        using var response = await after.PostAsync("/api/v1/me/bootstrap", null, TestContext.Current.CancellationToken);

        var profile = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        profile.GetProperty("email").GetString().ShouldBe("new@example.com");
        profile.GetProperty("emailVerified").GetBoolean().ShouldBeTrue();
        profile.GetProperty("displayName").GetString().ShouldBe("New Name");
    }

    [Fact]
    public async Task Bootstrap_records_when_the_user_was_last_seen()
    {
        using var client = fixture.CreateAuthenticatedClient("uid-seen");

        using var _ = await client.PostAsync("/api/v1/me/bootstrap", null, TestContext.Current.CancellationToken);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

        var user = await dbContext.Users.SingleAsync(
            candidate => candidate.FirebaseUid == "uid-seen",
            TestContext.Current.CancellationToken);

        user.LastSeenAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Concurrent_bootstraps_settle_on_one_account()
    {
        // A page load and a post-sign-in redirect can both fire this. The unique index turns
        // the loser into a DbUpdateException, which the handler retries rather than surfacing.
        using var client = fixture.CreateAuthenticatedClient("uid-race");

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ =>
                client.PostAsync("/api/v1/me/bootstrap", null, TestContext.Current.CancellationToken)));

        try
        {
            responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.OK);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuerioDbContext>();

        var count = await dbContext.Users.CountAsync(
            user => user.FirebaseUid == "uid-race",
            TestContext.Current.CancellationToken);

        count.ShouldBe(1);
    }

    [Fact]
    public async Task Two_sign_in_methods_for_one_address_are_two_accounts()
    {
        using var google = fixture.CreateAuthenticatedClient("google-uid", "person@example.com");
        using var password = fixture.CreateAuthenticatedClient("password-uid", "person@example.com");

        using var first = await google.PostAsync("/api/v1/me/bootstrap", null, TestContext.Current.CancellationToken);
        using var second = await password.PostAsync("/api/v1/me/bootstrap", null, TestContext.Current.CancellationToken);

        var firstProfile = await first.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var secondProfile = await second.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        // The documented account-linking limitation, exercised end to end.
        secondProfile.GetProperty("id").GetGuid()
            .ShouldNotBe(firstProfile.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Me_reports_not_provisioned_before_bootstrap_has_run()
    {
        using var client = fixture.CreateAuthenticatedClient("uid-unprovisioned");

        using var response = await client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // The client branches on this code to call bootstrap and retry, rather than showing
        // the user a dead end.
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errorCode").GetString().ShouldBe("user.not_provisioned");
    }

    [Fact]
    public async Task Me_returns_the_profile_once_provisioned()
    {
        using var client = fixture.CreateAuthenticatedClient("uid-me", "me@example.com", true, "Me");

        using var bootstrap = await client.PostAsync("/api/v1/me/bootstrap", null, TestContext.Current.CancellationToken);
        bootstrap.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        profile.GetProperty("email").GetString().ShouldBe("me@example.com");
    }

    [Fact]
    public async Task One_users_token_never_returns_another_users_profile()
    {
        using var alice = fixture.CreateAuthenticatedClient("uid-alice", "alice@example.com");
        using var bob = fixture.CreateAuthenticatedClient("uid-bob", "bob@example.com");

        using var _ = await alice.PostAsync("/api/v1/me/bootstrap", null, TestContext.Current.CancellationToken);
        using var __ = await bob.PostAsync("/api/v1/me/bootstrap", null, TestContext.Current.CancellationToken);

        using var response = await bob.GetAsync("/api/v1/me", TestContext.Current.CancellationToken);

        var profile = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        profile.GetProperty("email").GetString().ShouldBe("bob@example.com");
    }
}
