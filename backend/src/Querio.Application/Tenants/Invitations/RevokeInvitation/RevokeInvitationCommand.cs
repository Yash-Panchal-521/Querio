using Mediator;

namespace Querio.Application.Tenants.Invitations.RevokeInvitation;

public sealed record RevokeInvitationCommand(Guid TenantId, Guid InvitationId) : ICommand;
