using Mediator;
using Querio.Application.Tenants.Invitations;

namespace Querio.Application.Invitations.PreviewInvitation;

public sealed record PreviewInvitationQuery(string Token) : IQuery<InvitationPreview>;
