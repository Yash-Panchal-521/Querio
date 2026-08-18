using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Common.Errors;
using Querio.Domain.Users;

namespace Querio.Application.Common.Extensions;

/// <summary>
/// Bridges the authenticated token to the stored account. Every handler that acts on behalf
/// of a person goes through here, so "authenticated but never bootstrapped" produces one
/// consistent, actionable error rather than a null reference somewhere downstream.
/// </summary>
internal static class CurrentUserExtensions
{
    public static async Task<User> RequireProvisionedUserAsync(
        this ICurrentUser currentUser,
        IQuerioDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var firebaseUid = currentUser.FirebaseUid
            ?? throw new UnauthorizedException("Token does not identify a user.");

        return await dbContext.Users
            .FirstOrDefaultAsync(user => user.FirebaseUid == firebaseUid, cancellationToken)
            ?? throw new UserNotProvisionedException();
    }

    /// <summary>Id only, untracked — for read paths that never write the aggregate back.</summary>
    public static async Task<Guid> RequireUserIdAsync(
        this ICurrentUser currentUser,
        IQuerioDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var firebaseUid = currentUser.FirebaseUid
            ?? throw new UnauthorizedException("Token does not identify a user.");

        var userId = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.FirebaseUid == firebaseUid)
            .Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return userId ?? throw new UserNotProvisionedException();
    }
}
