using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Common.Errors;

namespace Querio.Application.Documents.GetDocument;

internal sealed class GetDocumentQueryHandler(IQuerioDbContext dbContext)
    : IQueryHandler<GetDocumentQuery, DocumentSummary>
{
    public async ValueTask<DocumentSummary> Handle(GetDocumentQuery query, CancellationToken cancellationToken) =>
        await dbContext.Documents
            .AsNoTracking()
            .Where(document => document.Id == query.DocumentId)
            .Select(document => new DocumentSummary(
                document.Id,
                document.FileName,
                document.Format,
                document.ByteSize,
                document.Status,
                document.ChunkCount,
                document.EmbeddedChunkCount,
                document.FailureCode,
                document.FailureReason,
                document.PauseReason,
                document.ResumesAt,
                document.UploadedByUserId,
                document.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("Document", query.DocumentId);
}
