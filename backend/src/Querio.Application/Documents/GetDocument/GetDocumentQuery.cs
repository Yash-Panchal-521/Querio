using Mediator;

namespace Querio.Application.Documents.GetDocument;

public sealed record GetDocumentQuery(Guid TenantId, Guid DocumentId) : IQuery<DocumentSummary>;
