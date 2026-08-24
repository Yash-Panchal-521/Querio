using Mediator;

namespace Querio.Application.Documents.UploadDocument;

/// <summary>
/// Carries the upload as a stream rather than as bytes. A twenty-megabyte array on an instance
/// with half a gigabyte of memory is a denial of service somebody else can trigger.
/// </summary>
public sealed record UploadDocumentCommand(
    Guid TenantId,
    string FileName,
    string ContentType,
    Stream Content) : ICommand<UploadDocumentResult>;

/// <summary>
/// <paramref name="AlreadyExisted"/> is what lets the endpoint answer 200 rather than 201 for
/// a file this organization already has, and lets the interface say so instead of appearing to
/// do nothing.
/// </summary>
public sealed record UploadDocumentResult(DocumentSummary Document, bool AlreadyExisted);
