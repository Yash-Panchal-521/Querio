using Querio.Domain.Common.Errors;

namespace Querio.Domain.Users;

/// <summary>
/// The caller holds a valid token but has no Querio profile yet.
///
/// Conflict rather than not-found: nothing is missing from the URL, the account is simply in
/// the wrong state. The distinct error code lets the client call bootstrap and retry instead
/// of showing the user a dead end.
/// </summary>
public sealed class UserNotProvisionedException()
    : QuerioException("This account has not been set up yet. Complete sign-in before continuing.")
{
    public override ErrorCategory Category => ErrorCategory.Conflict;

    public override string ErrorCode => "user.not_provisioned";
}
