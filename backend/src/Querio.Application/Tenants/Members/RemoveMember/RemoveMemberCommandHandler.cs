using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Application.Common.Extensions;
using Querio.Domain.Common.Errors;
using Querio.Domain.Tenants;

namespace Querio.Application.Tenants.Members.RemoveMember;

internal sealed class RemoveMemberCommandHandler(
    IQuerioDbContext dbContext,
    ICurrentUser currentUser) : ICommandHandler<RemoveMemberCommand>
{
    public async ValueTask<Unit> Handle(RemoveMemberCommand command, CancellationToken cancellationToken)
    {
        var actorId = await currentUser.RequireUserIdAsync(dbContext, cancellationToken);

        var tenant = await dbContext.Tenants
            .Include(candidate => candidate.Memberships)
            .FirstOrDefaultAsync(candidate => candidate.Id == command.TenantId, cancellationToken)
            ?? throw new NotFoundException("Organization", command.TenantId);

        var actor = tenant.MembershipFor(actorId)
            ?? throw new ForbiddenException();

        var target = tenant.MembershipFor(command.UserId)
            ?? throw new NotFoundException("Member", command.UserId);

        // The policy only established that the caller is at least an Admin. Admins may remove
        // Members but not each other: if they could, two Admins could remove one another and
        // the survivor would be whoever clicked first.
        if (actor.Role == TenantRole.Admin && target.Role >= TenantRole.Admin && target.UserId != actorId)
        {
            throw new ForbiddenException("Admins can only remove members.");
        }

        // Refuses to remove the final owner.
        tenant.RemoveMember(command.UserId);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
