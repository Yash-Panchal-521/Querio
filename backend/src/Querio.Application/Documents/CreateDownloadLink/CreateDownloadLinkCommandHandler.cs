using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Common.Errors;
using Querio.Domain.Documents;

namespace Querio.Application.Documents.CreateDownloadLink;

internal sealed class CreateDownloadLinkCommandHandler(
    IQuerioDbContext dbContext,
    IDocumentStorage storage,
    TimeProvider timeProvider) : ICommandHandler<CreateDownloadLinkCommand, DownloadLink>
{
    public async ValueTask<DownloadLink> Handle(
        CreateDownloadLinkCommand command,
        CancellationToken cancellationToken)
    {
        // Tenant-filtered, so asking for another organization's document reads as absent
        // rather than as refused — the same answer the rest of the API gives.
        var document = await dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == command.DocumentId, cancellationToken)
            ?? throw new NotFoundException("Document", command.DocumentId);

        var url = await storage.CreateDownloadLinkAsync(
            document.StorageKey,
            // The name they uploaded it under, not the content hash it is stored as. Accurate
            // and useless is still useless.
            document.FileName,
            DocumentLimits.DownloadLinkLifetime,
            cancellationToken);

        return new DownloadLink(url, timeProvider.GetUtcNow().Add(DocumentLimits.DownloadLinkLifetime));
    }
}
