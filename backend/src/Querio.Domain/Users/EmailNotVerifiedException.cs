using Querio.Domain.Common.Errors;

namespace Querio.Domain.Users;

/// <summary>
/// Raised when an action requires a proven email address.
///
/// Organization creation is gated on this because invitations are matched by email: if
/// someone could create an organization under an address they do not control, invitations
/// sent to that address would be trusted on their say-so. Accepting an invitation is
/// deliberately not gated — the inviter already asserted the address.
/// </summary>
public sealed class EmailNotVerifiedException()
    : QuerioException("Verify your email address before creating an organization.")
{
    public override ErrorCategory Category => ErrorCategory.Forbidden;

    public override string ErrorCode => "user.email_not_verified";
}
