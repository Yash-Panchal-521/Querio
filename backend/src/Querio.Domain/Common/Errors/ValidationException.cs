namespace Querio.Domain.Common.Errors;

/// <summary>
/// The request was well-formed but failed input or business validation. Field errors are
/// surfaced under the <c>errors</c> member of the error payload.
/// </summary>
public sealed class ValidationException : QuerioException
{
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public ValidationException(string field, string error)
        : this(new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [error] })
    {
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public override ErrorCategory Category => ErrorCategory.Validation;

    public override string ErrorCode => "request.validation_failed";

    public override IReadOnlyDictionary<string, object?> Extensions =>
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["errors"] = Errors };
}
