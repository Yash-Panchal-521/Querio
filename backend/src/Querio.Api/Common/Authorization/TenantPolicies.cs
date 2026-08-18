using Querio.Domain.Tenants;

namespace Querio.Api.Common.Authorization;

internal static class TenantPolicies
{
    public const string Member = "Tenant.Member";
    public const string Admin = "Tenant.Admin";
    public const string Owner = "Tenant.Owner";

    /// <summary>Route parameter every tenant-scoped endpoint carries.</summary>
    public const string TenantRouteKey = "tenantId";

    public static TenantRole MinimumRoleFor(string policyName) => policyName switch
    {
        Owner => TenantRole.Owner,
        Admin => TenantRole.Admin,
        _ => TenantRole.Member,
    };
}
