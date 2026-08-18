namespace Querio.Domain.Common.Errors;

/// <summary>The request collides with current state — duplicate slug, concurrent edit, already-ingested document.</summary>
public sealed class ConflictException : QuerioException
{
    public ConflictException(string message)
        : base(message)
    {
    }

    public ConflictException(string message, string errorCode)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;

    public override string ErrorCode { get; } = "resource.conflict";
}
