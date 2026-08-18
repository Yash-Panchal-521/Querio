using Mediator;
using Querio.Application.Tenants;

namespace Querio.Application.Invitations.AcceptInvitation;

public sealed record AcceptInvitationCommand(string Token) : ICommand<TenantSummary>;
