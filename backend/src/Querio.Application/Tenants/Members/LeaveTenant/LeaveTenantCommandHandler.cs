using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Application.Common.Extensions;
using Querio.Domain.Common.Errors;

namespace Querio.Application.Tenants.Members.LeaveTenant;

internal sealed class LeaveTenantCommandHandler(
    IQuerioDbContext dbContext,
    ICurrentUser currentUser) : ICommandHandler<LeaveTenantCommand>
{
    public async ValueTask<Unit> Handle(LeaveTenantCommand command, CancellationToken cancellationToken)
    {
        var userId = await currentUser.RequireUserIdAsync(dbContext, cancellationToken);

        var tenant = await dbContext.Tenants
            .Include(candidate => candidate.Memberships)
            .FirstOrDefaultAsync(candidate => candidate.Id == command.TenantId, cancellationToken)
            ?? throw new NotFoundException("Organization", command.TenantId);

        // Any member may leave — including an Owner, provided they are not the last one. The
        // aggregate enforces that, so leaving cannot strand an organization.
        tenant.RemoveMember(userId);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
