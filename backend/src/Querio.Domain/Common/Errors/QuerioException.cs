namespace Querio.Domain.Common.Errors;

/// <summary>
/// Base type for every failure Querio raises deliberately. Carrying the category and a
/// stable error code on the exception keeps the API's exception handler free of a type
/// switch that would otherwise grow with every feature.
/// </summary>
public abstract class QuerioException : Exception
{
    protected QuerioException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    /// <summary>How the failure should be classified for the caller.</summary>
    public abstract ErrorCategory Category { get; }

    /// <summary>Machine-readable code clients branch on, e.g. <c>document.not_found</c>.</summary>
    public abstract string ErrorCode { get; }

    /// <summary>Extra members merged into the error payload. Null when there are none.</summary>
    public virtual IReadOnlyDictionary<string, object?>? Extensions => null;
}
