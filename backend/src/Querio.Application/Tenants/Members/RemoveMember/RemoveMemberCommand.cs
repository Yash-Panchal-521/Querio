using Mediator;

namespace Querio.Application.Tenants.Members.RemoveMember;

public sealed record RemoveMemberCommand(Guid TenantId, Guid UserId) : ICommand;
