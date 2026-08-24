using Mediator;

namespace Querio.Application.Documents.DeleteDocument;

public sealed record DeleteDocumentCommand(Guid TenantId, Guid DocumentId) : ICommand;
