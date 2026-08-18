using Querio.Domain.Common;

namespace Querio.Domain.Users;

/// <summary>
/// A person, as Querio knows them.
///
/// Identity is keyed on <see cref="FirebaseUid"/>, never on email: Firebase issues a distinct
/// uid per sign-in method, so the same human signing in with Google and with a password is
/// two accounts. Email is therefore deliberately non-unique — see the account-linking
/// limitation in the auth stories.
/// </summary>
public sealed class User : Entity, IAuditable
{
    // EF materialisation.
    private User()
    {
        FirebaseUid = string.Empty;
        Email = string.Empty;
    }

    private User(string firebaseUid, string email, bool emailVerified, string? displayName)
    {
        FirebaseUid = firebaseUid;
        Email = email;
        EmailVerified = emailVerified;
        DisplayName = displayName;
    }

    /// <summary>Subject claim from the identity provider. Immutable for the life of the account.</summary>
    public string FirebaseUid { get; private set; }

    /// <summary>Stored lower-cased and trimmed so lookups need no case-insensitive collation.</summary>
    public string Email { get; private set; }

    public bool EmailVerified { get; private set; }

    public string? DisplayName { get; private set; }

    public DateTimeOffset? LastSeenAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static User Provision(string firebaseUid, string email, bool emailVerified, string? displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firebaseUid);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return new User(firebaseUid, NormalizeEmail(email), emailVerified, NormalizeDisplayName(displayName));
    }

    /// <summary>
    /// Re-applies the profile as the identity provider currently reports it. Called on every
    /// sign-in, so a changed email or a newly verified address is picked up without a
    /// separate sync job.
    /// </summary>
    /// <returns><c>true</c> when something actually changed, so callers can skip a pointless write.</returns>
    public bool RefreshProfile(string email, bool emailVerified, string? displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalizedEmail = NormalizeEmail(email);
        var normalizedDisplayName = NormalizeDisplayName(displayName);

        var changed = !string.Equals(Email, normalizedEmail, StringComparison.Ordinal)
            || EmailVerified != emailVerified
            || !string.Equals(DisplayName, normalizedDisplayName, StringComparison.Ordinal);

        if (!changed)
        {
            return false;
        }

        Email = normalizedEmail;
        EmailVerified = emailVerified;
        DisplayName = normalizedDisplayName;

        return true;
    }

    public void MarkSeen(DateTimeOffset seenAt) => LastSeenAt = seenAt;

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string? NormalizeDisplayName(string? displayName) =>
        string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
}
