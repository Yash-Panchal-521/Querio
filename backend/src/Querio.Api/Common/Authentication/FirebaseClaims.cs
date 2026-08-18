namespace Querio.Api.Common.Authentication;

/// <summary>Claim names exactly as Firebase issues them.</summary>
internal static class FirebaseClaims
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string EmailVerified = "email_verified";
    public const string Name = "name";
    public const string AuthTime = "auth_time";
}
