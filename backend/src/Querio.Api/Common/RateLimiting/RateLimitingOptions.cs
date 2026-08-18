namespace Querio.Api.Common.RateLimiting;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Called on every sign-in, so the ceiling is generous — this exists to stop a loop, not
    /// to ration normal use.
    /// </summary>
    public RateLimitWindow Bootstrap { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };

    /// <summary>
    /// Redeeming and previewing invitations. Tighter, because these are the only endpoints
    /// where a valid token from any Firebase project can probe for someone else's data.
    /// </summary>
    public RateLimitWindow InvitationRedemption { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };
}

public sealed class RateLimitWindow
{
    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; }

    public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);
}
