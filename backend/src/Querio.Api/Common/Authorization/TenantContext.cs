using Querio.Application.Common.Abstractions;

namespace Querio.Api.Common.Authorization;

/// <summary>
/// Set once per request by the authorization handler, after membership has been proven
/// against the database. Deliberately not settable from anywhere a request payload can
/// reach — the tenant is a conclusion, not an input.
/// </summary>
internal sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public void Establish(Guid tenantId) => TenantId = tenantId;
}
