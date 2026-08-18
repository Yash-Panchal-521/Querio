using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Common.Errors;

namespace Querio.Application.Tenants.DeleteTenant;

internal sealed class DeleteTenantCommandHandler(IQuerioDbContext dbContext)
    : ICommandHandler<DeleteTenantCommand>
{
    public async ValueTask<Unit> Handle(DeleteTenantCommand command, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(candidate => candidate.Id == command.TenantId, cancellationToken)
            ?? throw new NotFoundException("Organization", command.TenantId);

        // Memberships, invitations and later all uploaded content go with it, by cascade at
        // the database rather than by remembering to delete each table here.
        dbContext.Tenants.Remove(tenant);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
