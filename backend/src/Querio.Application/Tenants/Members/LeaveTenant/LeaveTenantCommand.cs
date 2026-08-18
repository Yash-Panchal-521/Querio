using Mediator;

namespace Querio.Application.Tenants.Members.LeaveTenant;

public sealed record LeaveTenantCommand(Guid TenantId) : ICommand;
