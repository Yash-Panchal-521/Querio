using Mediator;

namespace Querio.Application.Tenants.GetTenant;

public sealed record GetTenantQuery(Guid TenantId) : IQuery<TenantSummary>;
