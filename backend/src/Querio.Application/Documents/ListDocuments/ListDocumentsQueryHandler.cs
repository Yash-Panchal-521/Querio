using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;

namespace Querio.Application.Documents.ListDocuments;

internal sealed class ListDocumentsQueryHandler(IQuerioDbContext dbContext)
    : IQueryHandler<ListDocumentsQuery, IReadOnlyList<DocumentSummary>>
{
    public async ValueTask<IReadOnlyList<DocumentSummary>> Handle(
        ListDocumentsQuery query,
        CancellationToken cancellationToken) =>
        await dbContext.Documents
            .AsNoTracking()
            // Ids are UUID v7, so ordering by id is ordering by upload time — without a second
            // column in the index or a sort on a timestamp.
            .OrderByDescending(document => document.Id)
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
            .ToListAsync(cancellationToken);
}
