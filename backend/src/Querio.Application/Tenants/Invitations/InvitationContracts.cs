using Querio.Domain.Tenants;

namespace Querio.Application.Tenants.Invitations;

/// <summary>
/// Returned once, at the moment of issue. <see cref="Token"/> is the only copy that will ever
/// exist — the database keeps a hash — so the caller must hand it to the inviter immediately.
/// </summary>
public sealed record IssuedInvitation(
    Guid Id,
    string Email,
    TenantRole Role,
    DateTimeOffset ExpiresAt,
    string Token);

/// <summary>A pending invitation as an administrator sees it. Deliberately carries no token.</summary>
public sealed record InvitationSummary(
    Guid Id,
    string Email,
    TenantRole Role,
    DateTimeOffset ExpiresAt,
    DateTimeOffset InvitedAt);

/// <summary>
/// What the person holding a link is shown before signing in, so they know which
/// organization is asking and which address to use.
/// </summary>
public sealed record InvitationPreview(
    string OrganizationName,
    string Email,
    TenantRole Role,
    DateTimeOffset ExpiresAt);
