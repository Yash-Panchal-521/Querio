using Mediator;

namespace Querio.Application.Tenants.CreateTenant;

public sealed record CreateTenantCommand(string Name) : ICommand<TenantSummary>;
