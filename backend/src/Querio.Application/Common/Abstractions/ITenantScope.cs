namespace Querio.Application.Common.Abstractions;

/// <summary>
/// Establishes which organization the current unit of work belongs to.
///
/// Separate from <see cref="ITenantContext"/> on purpose. Reading the tenant is something
/// everything does; setting it is something exactly two callers may do — the authorization
/// handler, once membership has been proven against the database, and the ingestion worker,
/// once it has claimed a job that names its tenant. Keeping the setter off the read interface
/// is what stops a third caller appearing without anyone noticing.
/// </summary>
public interface ITenantScope
{
    void Establish(Guid tenantId);
}
