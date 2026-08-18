using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;

namespace Querio.Application.Tenants.Invitations.ListInvitations;

internal sealed class ListInvitationsQueryHandler(
    IQuerioDbContext dbContext,
    TimeProvider timeProvider) : IQueryHandler<ListInvitationsQuery, IReadOnlyList<InvitationSummary>>
{
    public async ValueTask<IReadOnlyList<InvitationSummary>> Handle(
        ListInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        // No explicit tenant predicate needed — the global filter already scopes this to the
        // organization the authorization layer established.
        var invitations = await dbContext.Invitations
            .AsNoTracking()
            .Where(invitation => invitation.AcceptedAt == null
                && invitation.RevokedAt == null
                && invitation.ExpiresAt > now)
            .OrderBy(invitation => invitation.CreatedAt)
            .Select(invitation => new InvitationSummary(
                invitation.Id,
                invitation.Email,
                invitation.Role,
                invitation.ExpiresAt,
                invitation.CreatedAt))
            .ToListAsync(cancellationToken);

        return invitations;
    }
}
