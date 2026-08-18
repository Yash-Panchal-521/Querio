using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;

namespace Querio.Application.Tenants.Members.ListMembers;

internal sealed class ListMembersQueryHandler(IQuerioDbContext dbContext)
    : IQueryHandler<ListMembersQuery, IReadOnlyList<MemberSummary>>
{
    public async ValueTask<IReadOnlyList<MemberSummary>> Handle(
        ListMembersQuery query,
        CancellationToken cancellationToken)
    {
        // Membership in this organization was already proven by the policy.
        var members = await dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.TenantId == query.TenantId)
            .Join(
                dbContext.Users.AsNoTracking(),
                membership => membership.UserId,
                user => user.Id,
                (membership, user) => new { Membership = membership, User = user })
            // Ordered on the source columns, before projecting: sorting by a property of the
            // projected record leaves EF unable to translate the expression to SQL.
            //
            // Owners first, then longest-serving, so the list reads as a hierarchy rather
            // than in whatever order Postgres happened to return it.
            .OrderByDescending(row => row.Membership.Role)
            .ThenBy(row => row.Membership.CreatedAt)
            .Select(row => new MemberSummary(
                row.User.Id,
                row.User.Email,
                row.User.DisplayName,
                row.Membership.Role,
                row.Membership.CreatedAt))
            .ToListAsync(cancellationToken);

        return members;
    }
}
