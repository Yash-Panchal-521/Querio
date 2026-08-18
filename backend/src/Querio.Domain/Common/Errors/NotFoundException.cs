namespace Querio.Domain.Common.Errors;

/// <summary>The requested resource does not exist, or is not visible to the caller's tenant.</summary>
public sealed class NotFoundException : QuerioException
{
    public NotFoundException(string resource, object key)
        : base($"{resource} '{key}' was not found.")
    {
        Resource = resource;
    }

    public NotFoundException(string message)
        : base(message)
    {
    }

    public string? Resource { get; }

    public override ErrorCategory Category => ErrorCategory.NotFound;

    public override string ErrorCode => "resource.not_found";
}
