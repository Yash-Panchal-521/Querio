namespace Querio.Domain.Common.Errors;

/// <summary>
/// The request was well-formed but failed input or business validation. Field errors are
/// surfaced under the <c>errors</c> member of the error payload.
/// </summary>
public sealed class ValidationException : QuerioException
{
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base(Summarise(errors))
    {
        Errors = errors;
    }

    public ValidationException(string field, string error)
        : this(new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [error] })
    {
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>
    /// One error is its own summary; several need a heading.
    ///
    /// This is the difference between a person reading "That file type is not supported" and
    /// reading "One or more validation errors occurred." The specific sentence was always here,
    /// in <see cref="Errors"/>, but the message is what becomes ProblemDetails' <c>detail</c>
    /// and therefore what a client shows — so a single-error failure was hiding its own answer
    /// behind a plural summary written for the case where there are many.
    /// </summary>
    private static string Summarise(IReadOnlyDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 1)
        {
            var only = errors.Single().Value;

            if (only is { Length: 1 } && !string.IsNullOrWhiteSpace(only[0]))
            {
                return only[0];
            }
        }

        return "One or more validation errors occurred.";
    }

    public override ErrorCategory Category => ErrorCategory.Validation;

    public override string ErrorCode => "request.validation_failed";

    public override IReadOnlyDictionary<string, object?> Extensions =>
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["errors"] = Errors };
}
