using Mediator;

namespace Querio.Application.Tenants.RenameTenant;

public sealed record RenameTenantCommand(Guid TenantId, string Name) : ICommand<TenantSummary>;
