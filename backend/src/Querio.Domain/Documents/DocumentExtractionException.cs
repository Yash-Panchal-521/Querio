namespace Querio.Domain.Documents;

/// <summary>
/// A file we cannot read, described in terms the person who uploaded it can act on.
///
/// Deliberately not a <c>QuerioException</c>: this is never thrown during a request. Ingestion
/// happens after the upload has already been accepted, so the failure has to be recorded
/// against the document and shown later, not returned as a status code to a caller who has
/// long since gone.
/// </summary>
public sealed class DocumentExtractionException(string failureCode, string userMessage)
    : Exception(userMessage)
{
    /// <summary>An encrypted PDF, which we could read only with the password.</summary>
    public const string Encrypted = "document.encrypted";

    /// <summary>Structurally fine and contains no text — almost always a scan.</summary>
    public const string NoText = "document.no_text";

    /// <summary>Malformed, truncated, or not what it claims to be.</summary>
    public const string Unreadable = "document.unreadable";

    /// <summary>Stable enough for the interface to branch on.</summary>
    public string FailureCode { get; } = failureCode;
}
