using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Application.Tenants.Invitations;
using Querio.Domain.Common.Errors;
using Querio.Domain.Tenants;

namespace Querio.Application.Invitations.PreviewInvitation;

internal sealed class PreviewInvitationQueryHandler(
    IQuerioDbContext dbContext,
    TimeProvider timeProvider) : IQueryHandler<PreviewInvitationQuery, InvitationPreview>
{
    public async ValueTask<InvitationPreview> Handle(
        PreviewInvitationQuery query,
        CancellationToken cancellationToken)
    {
        var tokenHash = InvitationToken.Hash(query.Token);
        var now = timeProvider.GetUtcNow();

        // IgnoreQueryFilters, deliberately: the caller is not a member yet, so there is no
        // established organization to filter by. Possession of the token is the authorization,
        // which is why it is 256 bits and stored only as a hash.
        var preview = await dbContext.Invitations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(invitation => invitation.TokenHash == tokenHash)
            .Join(
                dbContext.Tenants.AsNoTracking(),
                invitation => invitation.TenantId,
                tenant => tenant.Id,
                (invitation, tenant) => new
                {
                    tenant.Name,
                    invitation.Email,
                    invitation.Role,
                    invitation.ExpiresAt,
                    invitation.AcceptedAt,
                    invitation.RevokedAt,
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (preview is null)
        {
            throw new NotFoundException("This invitation link is not valid.");
        }

        if (preview.RevokedAt is not null)
        {
            throw new ConflictException("This invitation was revoked.", "invitation.revoked");
        }

        if (preview.AcceptedAt is not null)
        {
            throw new ConflictException("This invitation has already been used.", "invitation.already_accepted");
        }

        if (preview.ExpiresAt <= now)
        {
            throw new ConflictException("This invitation has expired. Ask for a new one.", "invitation.expired");
        }

        return new InvitationPreview(preview.Name, preview.Email, preview.Role, preview.ExpiresAt);
    }
}
