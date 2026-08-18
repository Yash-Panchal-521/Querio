using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Common.Errors;
using Querio.Domain.Tenants;

namespace Querio.Application.Tenants.RenameTenant;

internal sealed class RenameTenantCommandHandler(IQuerioDbContext dbContext)
    : ICommandHandler<RenameTenantCommand, TenantSummary>
{
    public async ValueTask<TenantSummary> Handle(
        RenameTenantCommand command,
        CancellationToken cancellationToken)
    {
        // Ownership was already proven by the authorization policy; reaching the handler
        // means the caller is an Owner of this organization.
        var tenant = await dbContext.Tenants
            .Include(candidate => candidate.Memberships)
            .FirstOrDefaultAsync(candidate => candidate.Id == command.TenantId, cancellationToken)
            ?? throw new NotFoundException("Organization", command.TenantId);

        tenant.Rename(command.Name);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new TenantSummary(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            TenantRole.Owner,
            tenant.Memberships.Count);
    }
}
