using Mediator;
using Querio.Domain.Tenants;

namespace Querio.Application.Tenants.Invitations.InviteMember;

public sealed record InviteMemberCommand(Guid TenantId, string Email, TenantRole Role)
    : ICommand<IssuedInvitation>;
