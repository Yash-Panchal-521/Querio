using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Querio.Api.Tests.Api;

/// <summary>
/// Mints RS256 tokens with a key the test host trusts.
///
/// The alternative — stubbing the authentication handler — would never execute the real
/// validation path, so a misconfigured audience would sail through every test. Given that
/// Firebase signs every project's tokens with the same shared Google keys, audience and
/// issuer validation is the entire security boundary, and it has to be the thing under test.
/// </summary>
public sealed class TestTokenIssuer : IDisposable
{
    public const string ProjectId = "querio-test-project";

    private readonly RSA rsa = RSA.Create(2048);

    public TestTokenIssuer() => SigningKey = new RsaSecurityKey(rsa) { KeyId = "querio-test-key" };

    public RsaSecurityKey SigningKey { get; }

    public static string Issuer => $"https://securetoken.google.com/{ProjectId}";

    /// <summary>A token that should be accepted: correct issuer, correct audience, in date.</summary>
    public string IssueFor(
        string firebaseUid,
        string email = "user@example.com",
        bool emailVerified = true,
        string? displayName = "Test User") =>
        Issue(new TokenRecipe
        {
            FirebaseUid = firebaseUid,
            Email = email,
            EmailVerified = emailVerified,
            DisplayName = displayName,
        });

    public string Issue(TokenRecipe recipe)
    {
        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["sub"] = recipe.FirebaseUid,
            // Firebase emits JSON booleans; serialising as a string here would let a bug that
            // only parses strings pass, so keep the real shape.
            ["email_verified"] = recipe.EmailVerified,
            ["auth_time"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };

        if (recipe.Email is not null)
        {
            claims["email"] = recipe.Email;
        }

        if (recipe.DisplayName is not null)
        {
            claims["name"] = recipe.DisplayName;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = recipe.Issuer ?? Issuer,
            Audience = recipe.Audience ?? ProjectId,
            Claims = claims,
            NotBefore = recipe.NotBefore ?? DateTime.UtcNow.AddMinutes(-1),
            Expires = recipe.Expires ?? DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(
                recipe.SigningKey ?? SigningKey,
                SecurityAlgorithms.RsaSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public void Dispose() => rsa.Dispose();

    public sealed class TokenRecipe
    {
        public required string FirebaseUid { get; init; }
        public string? Email { get; init; } = "user@example.com";
        public bool EmailVerified { get; init; } = true;
        public string? DisplayName { get; init; }
        public string? Issuer { get; init; }
        public string? Audience { get; init; }
        public DateTime? NotBefore { get; init; }
        public DateTime? Expires { get; init; }
        public SecurityKey? SigningKey { get; init; }
    }
}
