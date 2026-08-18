using Microsoft.AspNetCore.Authorization;
using Querio.Domain.Tenants;

namespace Querio.Api.Common.Authorization;

internal sealed class TenantRoleRequirement(TenantRole minimumRole) : IAuthorizationRequirement
{
    public TenantRole MinimumRole { get; } = minimumRole;
}

/// <summary>
/// Distinguishes "you are not in this organization" from "you are, but your role is too
/// low". The difference decides whether the caller sees 404 or 403 — see
/// <see cref="TenantAwareAuthorizationResultHandler"/>.
/// </summary>
internal static class TenantAuthorizationFailures
{
    public const string NotAMember = "tenant.not_a_member";
    public const string InsufficientRole = "tenant.insufficient_role";
}
