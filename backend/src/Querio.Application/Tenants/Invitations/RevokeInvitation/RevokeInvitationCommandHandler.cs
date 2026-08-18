using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Common.Errors;

namespace Querio.Application.Tenants.Invitations.RevokeInvitation;

internal sealed class RevokeInvitationCommandHandler(
    IQuerioDbContext dbContext,
    TimeProvider timeProvider) : ICommandHandler<RevokeInvitationCommand>
{
    public async ValueTask<Unit> Handle(RevokeInvitationCommand command, CancellationToken cancellationToken)
    {
        // The global filter confines this to the caller's organization, so an invitation id
        // from elsewhere simply is not found.
        var invitation = await dbContext.Invitations
            .FirstOrDefaultAsync(candidate => candidate.Id == command.InvitationId, cancellationToken)
            ?? throw new NotFoundException("Invitation", command.InvitationId);

        // Takes effect immediately rather than at expiry — the point of revoking a link
        // shared in error is that it stops working now.
        invitation.Revoke(timeProvider.GetUtcNow());

        await dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
