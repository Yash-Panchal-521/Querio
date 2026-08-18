using System.Net;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Querio.Api.Tests.Api;

[Collection(nameof(QuerioApiCollection))]
public sealed class AuthenticationTests(QuerioApiFixture fixture)
{
    private const string ProtectedRoute = "/api/v1/me";

    [Fact]
    public async Task Request_without_a_token_is_rejected()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync(ProtectedRoute, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_from_another_firebase_project_is_rejected()
    {
        // The one that matters. Firebase signs every project's tokens with the same Google
        // keys, so this token's signature is genuinely valid — only the audience differs.
        // If audience validation were ever relaxed, anyone with a free Firebase account could
        // authenticate here, and this is the test that would notice.
        var foreignToken = fixture.Tokens.Issue(new TestTokenIssuer.TokenRecipe
        {
            FirebaseUid = "attacker-uid",
            Audience = "someone-elses-project",
            Issuer = "https://securetoken.google.com/someone-elses-project",
        });

        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", foreignToken);

        using var response = await client.GetAsync(ProtectedRoute, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_with_our_issuer_but_a_foreign_audience_is_rejected()
    {
        // Isolates audience validation. The realistic cross-project token above differs in
        // BOTH issuer and audience, so issuer validation alone would reject it — meaning that
        // test cannot detect audience validation being switched off. This one can.
        var token = fixture.Tokens.Issue(new TestTokenIssuer.TokenRecipe
        {
            FirebaseUid = "attacker-uid",
            Audience = "someone-elses-project",
        });

        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        using var response = await client.GetAsync(ProtectedRoute, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_with_our_audience_but_a_foreign_issuer_is_rejected()
    {
        // Isolates issuer validation, for the same reason.
        var token = fixture.Tokens.Issue(new TestTokenIssuer.TokenRecipe
        {
            FirebaseUid = "attacker-uid",
            Issuer = "https://securetoken.google.com/someone-elses-project",
        });

        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        using var response = await client.GetAsync(ProtectedRoute, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var token = fixture.Tokens.Issue(new TestTokenIssuer.TokenRecipe
        {
            FirebaseUid = "expired-uid",
            NotBefore = DateTime.UtcNow.AddHours(-3),
            Expires = DateTime.UtcNow.AddHours(-2),
        });

        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        using var response = await client.GetAsync(ProtectedRoute, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_signed_by_an_untrusted_key_is_rejected()
    {
        using var attackerKey = RSA.Create(2048);

        var token = fixture.Tokens.Issue(new TestTokenIssuer.TokenRecipe
        {
            FirebaseUid = "forged-uid",
            SigningKey = new RsaSecurityKey(attackerKey),
        });

        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        using var response = await client.GetAsync(ProtectedRoute, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Garbage_bearer_value_is_rejected_without_a_server_error()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "not-a-token");

        using var response = await client.GetAsync(ProtectedRoute, TestContext.Current.CancellationToken);

        // A malformed token is the caller's problem, not a fault on our side.
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_endpoints_stay_open_to_unauthenticated_probes()
    {
        // Orchestrators cannot present a token; a probe behind auth would fail every deploy.
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
