using System.Net;
using System.Net.Http.Json;

namespace Querio.Api.Tests.Api;

[Collection(nameof(QuerioApiCollection))]
public sealed class AbuseProtectionTests(QuerioApiFixture fixture) : IAsyncLifetime
{
    private readonly TenantScenario scenario = new(fixture);

    public async ValueTask InitializeAsync() => await fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Hammering_bootstrap_is_throttled()
    {
        using var client = fixture.CreateAuthenticatedClient("uid-flood", "flood@example.com");

        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 40; attempt++)
        {
            using var response = await client.PostAsync(
                "/api/v1/me/bootstrap",
                null,
                TestContext.Current.CancellationToken);

            statuses.Add(response.StatusCode);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        statuses.ShouldContain(HttpStatusCode.TooManyRequests);

        // The early attempts must still have worked — a limit that rejects normal sign-in
        // would be worse than none.
        statuses[0].ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_throttled_response_says_when_to_come_back()
    {
        using var client = fixture.CreateAuthenticatedClient("uid-retry", "retry@example.com");

        HttpResponseMessage? throttled = null;

        try
        {
            for (var attempt = 0; attempt < 40; attempt++)
            {
                var response = await client.PostAsync(
                    "/api/v1/me/bootstrap",
                    null,
                    TestContext.Current.CancellationToken);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    throttled = response;

                    break;
                }

                response.Dispose();
            }

            throttled.ShouldNotBeNull();

            // Without Retry-After the client has to guess between a second and an hour.
            throttled.Headers.RetryAfter.ShouldNotBeNull();

            var problem = await TenantScenario.ReadAsync(throttled);
            problem.GetProperty("errorCode").GetString().ShouldBe("quota.rate_limited");
            problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
        }
        finally
        {
            throttled?.Dispose();
        }
    }

    [Fact]
    public async Task Guessing_invitation_tokens_is_throttled()
    {
        var stranger = await scenario.SignInAsync("uid-brute", "brute@example.com");

        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 30; attempt++)
        {
            using var response = await stranger.PostAsJsonAsync(
                "/api/v1/invitations/preview",
                new { Token = $"guess-{attempt}" },
                TestContext.Current.CancellationToken);

            statuses.Add(response.StatusCode);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        statuses.ShouldContain(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task One_caller_being_throttled_does_not_affect_another()
    {
        using var noisy = fixture.CreateAuthenticatedClient("uid-noisy", "noisy@example.com");

        for (var attempt = 0; attempt < 40; attempt++)
        {
            using var response = await noisy.PostAsync("/api/v1/me/bootstrap", null, TestContext.Current.CancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        // Partitioned per caller, so a single abusive account cannot deny service to everyone
        // else — which a global limiter would.
        using var quiet = fixture.CreateAuthenticatedClient("uid-quiet", "quiet@example.com");

        using var unaffected = await quiet.PostAsync("/api/v1/me/bootstrap", null, TestContext.Current.CancellationToken);

        unaffected.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task No_bearer_token_ever_reaches_the_log()
    {
        var token = fixture.Tokens.IssueFor("uid-logging", "logging@example.com");

        fixture.Logs.Clear();

        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        using var bootstrap = await client.PostAsync("/api/v1/me/bootstrap", null, TestContext.Current.CancellationToken);
        bootstrap.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var failure = await client.GetAsync("/api/v1/tenants/" + Guid.CreateVersion7(), TestContext.Current.CancellationToken);
        failure.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var entries = fixture.Logs.Entries;

        // Both a successful request and a failing one, because error paths log more.
        entries.ShouldNotBeEmpty();
        entries.ShouldAllBe(entry => !entry.Contains(token, StringComparison.Ordinal));
    }

    [Fact]
    public async Task No_invitation_token_ever_reaches_the_log()
    {
        var (owner, tenantId) = await scenario.OrganizationAsync("uid-logowner", "logowner@example.com", "Log Corp");

        var invitationToken = await TenantScenario.InviteAsync(owner, tenantId, "logged@example.com", "Member");

        fixture.Logs.Clear();

        var invitee = await scenario.SignInAsync("uid-loginvitee", "logged@example.com");

        using var accepted = await invitee.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new { Token = invitationToken },
            TestContext.Current.CancellationToken);

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);

        var entries = fixture.Logs.Entries;

        // Proves the sink really captured the request-path log — the exact place a token in
        // the URL would surface. Without this the assertion below could pass by capturing
        // nothing at all.
        entries.ShouldContain(entry => entry.Contains("/api/v1/invitations/accept", StringComparison.Ordinal));

        // This is why preview and accept take the token in the body: request logging records
        // RequestPath, so a token in the URL would be written to disk on every call.
        entries.ShouldAllBe(entry => !entry.Contains(invitationToken, StringComparison.Ordinal));
    }
}
