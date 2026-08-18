using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Application.Common.Extensions;
using Querio.Domain.Common.Errors;
using Querio.Domain.Tenants;
using Querio.Domain.Users;

namespace Querio.Application.Tenants.Invitations.InviteMember;

internal sealed class InviteMemberCommandHandler(
    IQuerioDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<InviteMemberCommand, IssuedInvitation>
{
    public async ValueTask<IssuedInvitation> Handle(
        InviteMemberCommand command,
        CancellationToken cancellationToken)
    {
        var actorId = await currentUser.RequireUserIdAsync(dbContext, cancellationToken);
        var email = User.NormalizeEmail(command.Email);
        var now = timeProvider.GetUtcNow();

        var tenant = await dbContext.Tenants
            .Include(candidate => candidate.Memberships)
            .FirstOrDefaultAsync(candidate => candidate.Id == command.TenantId, cancellationToken)
            ?? throw new NotFoundException("Organization", command.TenantId);

        var actor = tenant.MembershipFor(actorId) ?? throw new ForbiddenException();

        // The policy established the caller is at least an Admin. Only an Owner can create
        // another Owner — otherwise an Admin could promote themselves via a second account.
        if (actor.Role < TenantRole.Owner && command.Role >= TenantRole.Owner)
        {
            throw new ForbiddenException("Only owners can invite someone as an owner.");
        }

        await GuardAlreadyAMemberAsync(tenant, email, cancellationToken);
        await GuardAlreadyInvitedAsync(command.TenantId, email, now, cancellationToken);

        var (invitation, token) = Invitation.Issue(command.TenantId, email, command.Role, actorId, now);

        dbContext.Invitations.Add(invitation);

        await dbContext.SaveChangesAsync(cancellationToken);

        // The only moment the raw token exists outside the inviter's clipboard.
        return new IssuedInvitation(invitation.Id, invitation.Email, invitation.Role, invitation.ExpiresAt, token);
    }

    private async Task GuardAlreadyAMemberAsync(
        Tenant tenant,
        string email,
        CancellationToken cancellationToken)
    {
        var memberIds = tenant.Memberships.Select(membership => membership.UserId).ToArray();

        var alreadyAMember = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Email == email && memberIds.Contains(user.Id), cancellationToken);

        if (alreadyAMember)
        {
            throw new ConflictException(
                "That person is already a member of this organization.",
                "membership.already_exists");
        }
    }

    private async Task GuardAlreadyInvitedAsync(
        Guid tenantId,
        string email,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var pending = await dbContext.Invitations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                invitation => invitation.TenantId == tenantId
                    && invitation.Email == email
                    && invitation.AcceptedAt == null
                    && invitation.RevokedAt == null
                    && invitation.ExpiresAt > now,
                cancellationToken);

        if (pending is null)
        {
            return;
        }

        // The existing token cannot be shown again — only its hash was kept — so the honest
        // answer is to point at the live invitation and let the caller revoke and reissue.
        throw new ConflictException(
            "An invitation to that address is already pending. Revoke it first to send a new link.",
            "invitation.already_pending");
    }
}
