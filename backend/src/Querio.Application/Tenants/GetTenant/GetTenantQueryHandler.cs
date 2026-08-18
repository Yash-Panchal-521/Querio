using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Application.Common.Extensions;
using Querio.Domain.Common.Errors;

namespace Querio.Application.Tenants.GetTenant;

internal sealed class GetTenantQueryHandler(
    IQuerioDbContext dbContext,
    ICurrentUser currentUser) : IQueryHandler<GetTenantQuery, TenantSummary>
{
    public async ValueTask<TenantSummary> Handle(GetTenantQuery query, CancellationToken cancellationToken)
    {
        var userId = await currentUser.RequireUserIdAsync(dbContext, cancellationToken);

        // Joined through the caller's own membership rather than loaded by id and checked
        // afterwards: a query that cannot express "someone else's organization" cannot
        // accidentally return one.
        var summary = await dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId && membership.TenantId == query.TenantId)
            .Join(
                dbContext.Tenants.AsNoTracking(),
                membership => membership.TenantId,
                tenant => tenant.Id,
                (membership, tenant) => new TenantSummary(
                    tenant.Id,
                    tenant.Name,
                    tenant.Slug,
                    membership.Role,
                    tenant.Memberships.Count))
            .FirstOrDefaultAsync(cancellationToken);

        // Not-found rather than forbidden: confirming existence would reveal that a given
        // organization is a Querio customer.
        return summary ?? throw new NotFoundException("Organization", query.TenantId);
    }
}
