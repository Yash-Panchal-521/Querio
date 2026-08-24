using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Common.Errors;

namespace Querio.Application.Documents.ListDocumentChunks;

internal sealed class ListDocumentChunksQueryHandler(IQuerioDbContext dbContext)
    : IQueryHandler<ListDocumentChunksQuery, DocumentChunkPage>
{
    public async ValueTask<DocumentChunkPage> Handle(
        ListDocumentChunksQuery query,
        CancellationToken cancellationToken)
    {
        // Checked separately so a document that exists but has no passages yet reads as an
        // empty inspector rather than as a missing document.
        var exists = await dbContext.Documents
            .AsNoTracking()
            .AnyAsync(document => document.Id == query.DocumentId, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("Document", query.DocumentId);
        }

        var chunks = dbContext.DocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.DocumentId == query.DocumentId);

        var total = await chunks.CountAsync(cancellationToken);

        var page = await chunks
            .OrderBy(chunk => chunk.Ordinal)
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(chunk => new DocumentChunkView(
                chunk.Id,
                chunk.Ordinal,
                chunk.Text,
                chunk.Breadcrumb,
                chunk.PageNumber,
                chunk.ApproximateTokenCount,
                chunk.Embedding != null))
            .ToListAsync(cancellationToken);

        return new DocumentChunkPage(page, total);
    }
}
