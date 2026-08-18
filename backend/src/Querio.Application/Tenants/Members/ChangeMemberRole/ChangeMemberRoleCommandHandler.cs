using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Common.Errors;

namespace Querio.Application.Tenants.Members.ChangeMemberRole;

internal sealed class ChangeMemberRoleCommandHandler(IQuerioDbContext dbContext)
    : ICommandHandler<ChangeMemberRoleCommand>
{
    public async ValueTask<Unit> Handle(ChangeMemberRoleCommand command, CancellationToken cancellationToken)
    {
        // Loaded with its memberships because the last-owner rule is a property of the whole
        // organization, not of the row being edited.
        var tenant = await dbContext.Tenants
            .Include(candidate => candidate.Memberships)
            .FirstOrDefaultAsync(candidate => candidate.Id == command.TenantId, cancellationToken)
            ?? throw new NotFoundException("Organization", command.TenantId);

        // Refuses to demote the final owner.
        tenant.ChangeRole(command.UserId, command.Role);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
