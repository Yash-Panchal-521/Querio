using Mediator;

namespace Querio.Application.Tenants.DeleteTenant;

public sealed record DeleteTenantCommand(Guid TenantId) : ICommand;
