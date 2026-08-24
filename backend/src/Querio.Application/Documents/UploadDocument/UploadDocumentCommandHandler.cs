using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Querio.Application.Common.Abstractions;
using Querio.Application.Common.Extensions;
using Querio.Domain.Common.Errors;
using Querio.Domain.Documents;

namespace Querio.Application.Documents.UploadDocument;

internal sealed class UploadDocumentCommandHandler(
    IQuerioDbContext dbContext,
    IDocumentStorage storage,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<UploadDocumentCommand, UploadDocumentResult>
{
    public async ValueTask<UploadDocumentResult> Handle(
        UploadDocumentCommand command,
        CancellationToken cancellationToken)
    {
        var uploaderId = await currentUser.RequireUserIdAsync(dbContext, cancellationToken);

        // Buffered to disk rather than memory, and to a file this method owns exclusively. The
        // content has to be read twice — once to hash and identify it, once to store it — and
        // the incoming request stream cannot be rewound.
        var scratchPath = Path.Combine(
            Path.GetTempPath(),
            string.Create(CultureInfo.InvariantCulture, $"querio-upload-{Guid.CreateVersion7():N}"));

        try
        {
            var (contentHash, byteSize, format) = await AbsorbAsync(command, scratchPath, cancellationToken);

            var existing = await dbContext.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    document => document.TenantId == command.TenantId && document.ContentHash == contentHash,
                    cancellationToken);

            if (existing is not null)
            {
                // Returning the document they already have, rather than refusing. Embedding is
                // the metered resource, and a second copy would spend it to produce vectors
                // identical to ones already stored.
                return new UploadDocumentResult(Describe(existing), AlreadyExisted: true);
            }

            await GuardQuotaAsync(command.TenantId, byteSize, cancellationToken);

            await using (var stored = File.OpenRead(scratchPath))
            {
                // Storage before the row, deliberately. This way a crash between the two leaves
                // an unreferenced object — invisible, and overwritten by the next attempt at the
                // same content-addressed key. The other order would leave a document whose bytes
                // do not exist, which the interface would have to explain.
                var storageKey = await storage.StoreAsync(
                    command.TenantId,
                    contentHash,
                    stored,
                    command.ContentType,
                    cancellationToken);

                var document = Document.Record(
                    command.TenantId,
                    uploaderId,
                    command.FileName,
                    format,
                    byteSize,
                    contentHash,
                    storageKey);

                dbContext.Documents.Add(document);

                // Same SaveChanges, so the same transaction: a document can never exist without
                // work queued to ingest it, and no second system has to be running for that to
                // hold.
                dbContext.IngestionJobs.Add(
                    IngestionJob.QueueIngestion(command.TenantId, document.Id, timeProvider.GetUtcNow()));

                await dbContext.SaveChangesAsync(cancellationToken);

                return new UploadDocumentResult(Describe(document), AlreadyExisted: false);
            }
        }
        finally
        {
            if (File.Exists(scratchPath))
            {
                File.Delete(scratchPath);
            }
        }
    }

    /// <summary>
    /// Streams the upload to disk while hashing it and sampling its opening bytes, so the file
    /// is read once rather than three times.
    /// </summary>
    private static async Task<(string ContentHash, long ByteSize, FileFormat Format)> AbsorbAsync(
        UploadDocumentCommand command,
        string scratchPath,
        CancellationToken cancellationToken)
    {
        var prefix = new byte[DocumentFormatDetection.PrefixBytes];
        var prefixLength = 0;
        long byteSize = 0;

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(81920);

        try
        {
            await using var scratch = new FileStream(
                scratchPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            int read;
            while ((read = await command.Content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                byteSize += read;

                // Checked as it arrives, not after. Waiting for the end would mean writing the
                // whole oversized file to disk before rejecting it.
                if (byteSize > DocumentLimits.MaxFileBytes)
                {
                    throw new ValidationException(
                        "file",
                        $"Files must be {DocumentLimits.MaxFileBytes / (1024 * 1024)} MB or smaller.");
                }

                if (prefixLength < prefix.Length)
                {
                    var take = Math.Min(prefix.Length - prefixLength, read);
                    buffer.AsSpan(0, take).CopyTo(prefix.AsSpan(prefixLength));
                    prefixLength += take;
                }

                hasher.AppendData(buffer, 0, read);
                await scratch.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        if (byteSize == 0)
        {
            throw new ValidationException("file", "The file is empty.");
        }

        if (!DocumentFormatDetection.TryDetect(prefix.AsSpan(0, prefixLength), command.FileName, out var format))
        {
            throw new ValidationException(
                "file",
                "That file type is not supported. Upload a PDF, Word document, Markdown or plain text file.");
        }

        return (Convert.ToHexStringLower(hasher.GetHashAndReset()), byteSize, format);
    }

    private async Task GuardQuotaAsync(Guid tenantId, long incomingBytes, CancellationToken cancellationToken)
    {
        var usage = await dbContext.Documents
            .AsNoTracking()
            .Where(document => document.TenantId == tenantId)
            .GroupBy(_ => 1)
            .Select(group => new { Count = group.Count(), Bytes = group.Sum(document => document.ByteSize) })
            .FirstOrDefaultAsync(cancellationToken);

        var count = usage?.Count ?? 0;
        var bytes = usage?.Bytes ?? 0;

        if (count >= DocumentLimits.MaxDocumentsPerTenant)
        {
            throw new ConflictException(
                $"This organization has reached its limit of {DocumentLimits.MaxDocumentsPerTenant} documents. Delete one to upload another.",
                "tenant.document_limit_reached");
        }

        if (bytes + incomingBytes > DocumentLimits.MaxStoredBytesPerTenant)
        {
            throw new ConflictException(
                $"This organization has used its {DocumentLimits.MaxStoredBytesPerTenant / (1024 * 1024)} MB of storage. Delete something to make room.",
                "tenant.storage_limit_reached");
        }
    }

    private static DocumentSummary Describe(Document document) =>
        new(
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
            document.CreatedAt);
}
