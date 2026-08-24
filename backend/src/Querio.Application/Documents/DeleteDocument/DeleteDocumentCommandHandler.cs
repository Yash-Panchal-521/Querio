using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Querio.Application.Common.Abstractions;
using Querio.Application.Common.Extensions;
using Querio.Domain.Common.Errors;
using Querio.Domain.Documents;
using Querio.Domain.Tenants;

namespace Querio.Application.Documents.DeleteDocument;

internal sealed partial class DeleteDocumentCommandHandler(
    IQuerioDbContext dbContext,
    IDocumentStorage storage,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<DeleteDocumentCommandHandler> logger) : ICommandHandler<DeleteDocumentCommand>
{
    public async ValueTask<Unit> Handle(DeleteDocumentCommand command, CancellationToken cancellationToken)
    {
        var actorId = await currentUser.RequireUserIdAsync(dbContext, cancellationToken);

        var document = await dbContext.Documents
            .FirstOrDefaultAsync(candidate => candidate.Id == command.DocumentId, cancellationToken)
            ?? throw new NotFoundException("Document", command.DocumentId);

        var tenant = await dbContext.Tenants
            .Include(candidate => candidate.Memberships)
            .FirstOrDefaultAsync(candidate => candidate.Id == command.TenantId, cancellationToken)
            ?? throw new NotFoundException("Organization", command.TenantId);

        var actor = tenant.MembershipFor(actorId) ?? throw new ForbiddenException();

        // People can remove what they uploaded; clearing up after someone else is an
        // administrative act. Both are members of the organization, so the policy on the
        // endpoint cannot express this on its own.
        if (document.UploadedByUserId != actorId && actor.Role < TenantRole.Admin)
        {
            throw new ForbiddenException("Only an administrator can delete a document someone else uploaded.");
        }

        var storageKey = document.StorageKey;

        // The row goes first. A failure after this leaves an object nothing references, which
        // costs storage and is invisible; the other order would leave a document listed in the
        // interface whose bytes no longer exist, which is a broken download and a failing
        // re-ingestion. Chunks and the ingestion job cascade with it.
        dbContext.Documents.Remove(document);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await storage.DeleteAsync(storageKey, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Not rethrown: the document is gone as far as the caller is concerned, and failing
            // their request now would be inaccurate.
            //
            // Queued rather than merely logged. A log line records that storage now holds
            // something nothing references; a job actually removes it, with the same leasing,
            // backoff and crash-safety ingestion already has.
            LogOrphanedObject(logger, storageKey, exception);

            dbContext.IngestionJobs.Add(
                IngestionJob.QueueObjectDeletion(command.TenantId, storageKey, timeProvider.GetUtcNow()));

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Unit.Value;
    }

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Error,
        Message = "Document row deleted but its stored object remains at {StorageKey}; a cleanup job has been queued for it.")]
    private static partial void LogOrphanedObject(ILogger logger, string storageKey, Exception exception);
}
