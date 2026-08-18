using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Common.Errors;
using Querio.Domain.Users;

namespace Querio.Application.Users.GetCurrentUser;

internal sealed class GetCurrentUserQueryHandler(
    IQuerioDbContext dbContext,
    ICurrentUser currentUser) : IQueryHandler<GetCurrentUserQuery, UserProfile>
{
    public async ValueTask<UserProfile> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        var firebaseUid = currentUser.FirebaseUid
            ?? throw new UnauthorizedException("Token does not identify a user.");

        // Projected rather than tracked: this is a read, and materialising the aggregate
        // would let a later change accidentally save from a query path.
        var account = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.FirebaseUid == firebaseUid)
            .Select(user => new
            {
                user.Id,
                user.Email,
                user.EmailVerified,
                user.DisplayName,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new UserNotProvisionedException();

        var organizations = await UserOrganizationsQuery.ForUserAsync(dbContext, account.Id, cancellationToken);

        return new UserProfile(
            account.Id,
            account.Email,
            account.EmailVerified,
            account.DisplayName,
            organizations);
    }
}
