using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Application.Tenants;

namespace Querio.Application.Users;

internal static class UserOrganizationsQuery
{
    /// <summary>
    /// The caller's organizations, oldest membership first.
    ///
    /// The order is deliberately stable rather than "most recently used": Querio does not
    /// remember which organization someone was last in, so the client must land on the same
    /// one every time or people would find themselves somewhere different on each sign-in.
    /// </summary>
    public static Task<List<TenantSummary>> ForUserAsync(
        IQuerioDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .OrderBy(membership => membership.CreatedAt)
            .ThenBy(membership => membership.Id)
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
            .ToListAsync(cancellationToken);
}
