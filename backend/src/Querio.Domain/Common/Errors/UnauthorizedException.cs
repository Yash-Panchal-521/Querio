namespace Querio.Domain.Common.Errors;

/// <summary>No usable credential was presented, or the token failed validation.</summary>
public sealed class UnauthorizedException : QuerioException
{
    public UnauthorizedException(string message = "Authentication is required to access this resource.")
        : base(message)
    {
    }

    public override ErrorCategory Category => ErrorCategory.Unauthorized;

    public override string ErrorCode => "access.unauthorized";
}
