using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Common.Errors;
using Querio.Domain.Users;

namespace Querio.Application.Users.BootstrapCurrentUser;

internal sealed class BootstrapCurrentUserCommandHandler(
    IQuerioDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<BootstrapCurrentUserCommand, UserProfile>
{
    public async ValueTask<UserProfile> Handle(
        BootstrapCurrentUserCommand command,
        CancellationToken cancellationToken)
    {
        var firebaseUid = currentUser.FirebaseUid
            ?? throw new UnauthorizedException("Token does not identify a user.");

        var email = currentUser.Email
            ?? throw new UnauthorizedException("Token does not carry an email address.");

        try
        {
            return await UpsertAsync(firebaseUid, email, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two sign-ins can race here — a page load and a redirect both calling bootstrap.
            // The unique index on firebase_uid is what makes that safe, and losing the race is
            // not an error: re-read and apply the refresh to the row that won.
            dbContext.ChangeTracker.Clear();

            return await UpsertAsync(firebaseUid, email, cancellationToken);
        }
    }

    private async Task<UserProfile> UpsertAsync(
        string firebaseUid,
        string email,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(candidate => candidate.FirebaseUid == firebaseUid, cancellationToken);

        if (user is null)
        {
            user = User.Provision(firebaseUid, email, currentUser.EmailVerified, currentUser.DisplayName);

            dbContext.Users.Add(user);
        }
        else
        {
            // Picks up a changed display name, or an address verified since last sign-in,
            // without a separate synchronisation job.
            user.RefreshProfile(email, currentUser.EmailVerified, currentUser.DisplayName);
        }

        user.MarkSeen(timeProvider.GetUtcNow());

        await dbContext.SaveChangesAsync(cancellationToken);

        var organizations = await UserOrganizationsQuery.ForUserAsync(dbContext, user.Id, cancellationToken);

        return new UserProfile(user.Id, user.Email, user.EmailVerified, user.DisplayName, organizations);
    }
}
