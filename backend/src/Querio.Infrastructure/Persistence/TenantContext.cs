using Querio.Application.Common.Abstractions;

namespace Querio.Infrastructure.Persistence;

/// <summary>
/// The organization the current unit of work is acting in.
///
/// Lives beside the DbContext that consults it rather than in the API, because it is not an
/// HTTP concern: a request establishes it after proving membership, and the ingestion worker
/// establishes it after claiming a job. Both then read tenant-owned data through the same
/// default-deny filters.
///
/// Settable only through <see cref="ITenantScope"/>, so nothing that merely reads the tenant
/// can change it — the tenant is a conclusion, never an input.
/// </summary>
internal sealed class TenantContext : ITenantContext, ITenantScope
{
    public Guid? TenantId { get; private set; }

    public void Establish(Guid tenantId) => TenantId = tenantId;
}
