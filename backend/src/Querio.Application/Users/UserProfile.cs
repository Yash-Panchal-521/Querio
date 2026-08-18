using Querio.Application.Tenants;

namespace Querio.Application.Users;

/// <summary>
/// The caller's own profile, with the organizations they can act in.
///
/// Returned by both bootstrap and /me so the client has everything it needs to route after
/// sign-in: no organizations means send them to create-or-join.
/// </summary>
public sealed record UserProfile(
    Guid Id,
    string Email,
    bool EmailVerified,
    string? DisplayName,
    IReadOnlyList<TenantSummary> Organizations);
