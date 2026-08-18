using Querio.Domain.Common;
using Querio.Domain.Common.Errors;
using Querio.Domain.Users;

namespace Querio.Domain.Tenants;

/// <summary>
/// An offer of membership, bound to the address it was sent to and usable once.
///
/// Binding to the address means a forwarded link is useless to anyone else — the person
/// redeeming it must already have proven that address to Firebase.
/// </summary>
public sealed class Invitation : Entity, IAuditable, IHasTenant
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private Invitation()
    {
        Email = string.Empty;
        TokenHash = [];
    }

    private Invitation(
        Guid tenantId,
        string email,
        TenantRole role,
        byte[] tokenHash,
        Guid invitedByUserId,
        DateTimeOffset expiresAt)
    {
        TenantId = tenantId;
        Email = email;
        Role = role;
        TokenHash = tokenHash;
        InvitedByUserId = invitedByUserId;
        ExpiresAt = expiresAt;
    }

    public Guid TenantId { get; private set; }

    public string Email { get; private set; }

    public TenantRole Role { get; private set; }

    /// <summary>SHA-256 of the token. The token itself is never persisted.</summary>
    public byte[] TokenHash { get; private set; }

    public Guid InvitedByUserId { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public Guid? AcceptedByUserId { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Returns the invitation and the one and only copy of its token. The caller must hand
    /// the token straight to the inviter; nothing can recover it afterwards.
    /// </summary>
    public static (Invitation Invitation, string Token) Issue(
        Guid tenantId,
        string email,
        TenantRole role,
        Guid invitedByUserId,
        DateTimeOffset issuedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var token = InvitationToken.Create();

        var invitation = new Invitation(
            tenantId,
            User.NormalizeEmail(email),
            role,
            InvitationToken.Hash(token),
            invitedByUserId,
            issuedAt.Add(Lifetime));

        return (invitation, token);
    }

    public bool IsPending(DateTimeOffset asOf) =>
        AcceptedAt is null && RevokedAt is null && ExpiresAt > asOf;

    /// <summary>
    /// Redeems the invitation for a specific account. Every rejection carries a distinct code
    /// so the interface can say which of the four things went wrong rather than "invalid link".
    /// </summary>
    public void Accept(Guid userId, string userEmail, DateTimeOffset acceptedAt)
    {
        if (RevokedAt is not null)
        {
            throw new ConflictException("This invitation was revoked.", "invitation.revoked");
        }

        if (AcceptedAt is not null)
        {
            throw new ConflictException("This invitation has already been used.", "invitation.already_accepted");
        }

        if (ExpiresAt <= acceptedAt)
        {
            throw new ConflictException("This invitation has expired. Ask for a new one.", "invitation.expired");
        }

        if (!string.Equals(Email, User.NormalizeEmail(userEmail), StringComparison.Ordinal))
        {
            // Deliberately does not reveal the invited address to a stranger holding the link.
            throw new ForbiddenException("This invitation was sent to a different email address.");
        }

        AcceptedAt = acceptedAt;
        AcceptedByUserId = userId;
    }

    public void Revoke(DateTimeOffset revokedAt)
    {
        if (AcceptedAt is not null)
        {
            throw new ConflictException(
                "This invitation was already accepted. Remove the member instead.",
                "invitation.already_accepted");
        }

        // Revoking twice is not a failure worth surfacing.
        RevokedAt ??= revokedAt;
    }
}
