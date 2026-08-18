namespace Querio.Domain.Common.Errors;

/// <summary>
/// Transport-independent classification of a failure. The API layer maps these to HTTP
/// status codes; nothing below it should know what an HTTP status is.
/// </summary>
public enum ErrorCategory
{
    Unexpected = 0,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    RateLimited,
    Timeout,
    Unavailable,
}
