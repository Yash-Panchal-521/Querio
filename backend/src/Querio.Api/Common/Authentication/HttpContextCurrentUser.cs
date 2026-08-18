using System.Security.Claims;
using Querio.Application.Common.Abstractions;

namespace Querio.Api.Common.Authentication;

/// <summary>
/// Exposes the verified token to the application layer without handing it an HttpContext.
/// Everything here comes from a signature-checked token, so no value needs re-validating —
/// but none of it says anything about which organizations the caller may act in. That is
/// decided against the database, never against a claim.
/// </summary>
internal sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public string? FirebaseUid => Claim(FirebaseClaims.Subject);

    public string? Email => Claim(FirebaseClaims.Email);

    /// <summary>
    /// JWT booleans arrive as the strings "true"/"false", so a plain cast silently yields
    /// false and would gate verified users out of organization creation.
    /// </summary>
    public bool EmailVerified =>
        bool.TryParse(Claim(FirebaseClaims.EmailVerified), out var verified) && verified;

    public string? DisplayName => Claim(FirebaseClaims.Name);

    private string? Claim(string type)
    {
        var value = Principal?.FindFirst(type)?.Value;

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
