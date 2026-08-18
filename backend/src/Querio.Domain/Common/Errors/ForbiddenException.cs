namespace Querio.Domain.Common.Errors;

/// <summary>
/// The caller is authenticated but not allowed to perform this action — wrong tenant, or a
/// role that does not carry the permission.
/// </summary>
public sealed class ForbiddenException : QuerioException
{
    public ForbiddenException(string message = "You do not have permission to perform this action.")
        : base(message)
    {
    }

    public override ErrorCategory Category => ErrorCategory.Forbidden;

    public override string ErrorCode => "access.forbidden";
}
