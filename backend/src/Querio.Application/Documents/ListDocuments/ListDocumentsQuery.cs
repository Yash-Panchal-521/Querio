using Mediator;

namespace Querio.Application.Documents.ListDocuments;

public sealed record ListDocumentsQuery(Guid TenantId) : IQuery<IReadOnlyList<DocumentSummary>>;
