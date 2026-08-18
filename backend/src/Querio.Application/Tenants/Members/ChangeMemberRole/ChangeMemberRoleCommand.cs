using Mediator;
using Querio.Domain.Tenants;

namespace Querio.Application.Tenants.Members.ChangeMemberRole;

public sealed record ChangeMemberRoleCommand(Guid TenantId, Guid UserId, TenantRole Role) : ICommand;
