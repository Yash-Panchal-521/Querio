using Mediator;

namespace Querio.Application.Tenants.Invitations.ListInvitations;

public sealed record ListInvitationsQuery(Guid TenantId) : IQuery<IReadOnlyList<InvitationSummary>>;
