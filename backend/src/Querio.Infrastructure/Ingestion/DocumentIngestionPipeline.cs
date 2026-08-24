using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Documents;
using Querio.Infrastructure.Persistence;

namespace Querio.Infrastructure.Ingestion;

/// <summary>
/// Takes one claimed document from stored bytes to searchable passages.
///
/// Every transition is saved as it happens rather than at the end, because the interface polls
/// this row to answer "is it ready yet" and a status that only becomes true at the finish is
/// indistinguishable from one that is stuck.
/// </summary>
internal sealed partial class DocumentIngestionPipeline(
    QuerioDbContext dbContext,
    IDocumentStorage storage,
    IEnumerable<ITextExtractor> extractors,
    IChunker chunker,
    IEmbeddingService embeddings,
    ILogger<DocumentIngestionPipeline> logger)
{
    public async Task RunAsync(Guid documentId, Func<Task> renewLease, CancellationToken cancellationToken)
    {
        var document = await dbContext.Documents
            .FirstOrDefaultAsync(candidate => candidate.Id == documentId, cancellationToken);

        if (document is null)
        {
            // Deleted while it sat in the queue. Nothing to do, and nothing wrong.
            LogDocumentGone(logger, documentId);

            return;
        }

        var extracted = await ExtractAsync(document, cancellationToken);

        document.BeginChunking();
        await dbContext.SaveChangesAsync(cancellationToken);

        var passages = chunker.Chunk(extracted);

        if (passages.Count == 0)
        {
            throw new DocumentExtractionException(
                DocumentExtractionException.NoText,
                "No readable text was found in this document. If it is a scan, it needs to be converted to text first.");
        }

        var resumeFrom = await ResumePointAsync(document, passages.Count, cancellationToken);

        if (resumeFrom == 0)
        {
            // Cleared before writing, so a retry replaces rather than duplicates. The unique
            // index on (document_id, ordinal) would catch a duplicate, but failing the whole
            // job to discover that would waste the attempt.
            await dbContext.DocumentChunks
                .Where(chunk => chunk.DocumentId == document.Id)
                .ExecuteDeleteAsync(cancellationToken);
        }

        document.BeginEmbedding(passages.Count, resumeFrom);
        await dbContext.SaveChangesAsync(cancellationToken);

        await EmbedAsync(document, passages, resumeFrom, renewLease, cancellationToken);

        document.MarkReady();
        await dbContext.SaveChangesAsync(cancellationToken);

        LogIngested(logger, document.Id, passages.Count);
    }

    private async Task<ExtractedText> ExtractAsync(Document document, CancellationToken cancellationToken)
    {
        document.BeginExtracting();
        await dbContext.SaveChangesAsync(cancellationToken);

        var extractor = extractors.FirstOrDefault(candidate => candidate.Format == document.Format)
            ?? throw new DocumentExtractionException(
                DocumentExtractionException.Unreadable,
                "This file type can no longer be processed.");

        // Copied to disk first. The extractors need to seek — a PDF's cross-reference table is
        // at the end of the file — and an object-storage response stream cannot.
        var scratchPath = Path.Combine(
            Path.GetTempPath(),
            string.Create(CultureInfo.InvariantCulture, $"querio-ingest-{document.Id:N}"));

        try
        {
            await using (var source = await storage.OpenAsync(document.StorageKey, cancellationToken))
            await using (var scratch = new FileStream(
                scratchPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await source.CopyToAsync(scratch, cancellationToken);
            }

            await using var content = new FileStream(
                scratchPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

            var extracted = await extractor.ExtractAsync(content, cancellationToken);

            if (extracted.IsEmpty)
            {
                // Structurally valid and containing nothing to embed. Almost always a scan, and
                // saying so is far more use than reporting a document that succeeded and then
                // answers nothing.
                throw new DocumentExtractionException(
                    DocumentExtractionException.NoText,
                    "No readable text was found in this document. If it is a scan, it needs to be converted to text first.");
            }

            return extracted;
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
    /// How many passages are already embedded and can be left alone.
    ///
    /// This is what makes a paused document finishable. Embedding is metered per minute, so a
    /// long document is paused part-way through as a matter of course rather than as a fault —
    /// and starting over each time it resumes means re-spending the allowance on work already
    /// done, which for anything longer than a minute's worth of passages never terminates.
    ///
    /// Only a contiguous prefix counts, and only when this run chunked the document the same
    /// way as the last one. Chunks are written with their vector already attached, so an
    /// existing row is always a finished one and the prefix is simply how many there are;
    /// anything else — a different passage count, a gap in the ordinals — means the text no
    /// longer lines up with the vectors, and the honest thing is to start again.
    /// </summary>
    private async Task<int> ResumePointAsync(
        Document document,
        int passageCount,
        CancellationToken cancellationToken)
    {
        if (document.ChunkCount != passageCount)
        {
            return 0;
        }

        var existing = dbContext.DocumentChunks.Where(chunk => chunk.DocumentId == document.Id);

        var count = await existing.CountAsync(cancellationToken);

        if (count == 0 || count > passageCount)
        {
            return 0;
        }

        var highest = await existing.MaxAsync(chunk => chunk.Ordinal, cancellationToken);

        return highest == count - 1 ? count : 0;
    }

    private async Task EmbedAsync(
        Document document,
        IReadOnlyList<TextChunk> passages,
        int resumeFrom,
        Func<Task> renewLease,
        CancellationToken cancellationToken)
    {
        var embedded = resumeFrom;

        if (resumeFrom > 0)
        {
            LogResumed(logger, document.Id, resumeFrom, passages.Count);
        }

        foreach (var batch in passages.Skip(resumeFrom).Chunk(embeddings.MaxBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The breadcrumb goes to the provider but is not stored in the passage text. A
            // passage that opens with "Handbook › Leave › Parental" embeds closer to questions
            // about parental leave, while the stored text stays what the document actually says.
            var inputs = batch
                .Select(passage => passage.Breadcrumb is null
                    ? passage.Text
                    : $"{passage.Breadcrumb}\n\n{passage.Text}")
                .ToArray();

            var vectors = await embeddings.EmbedDocumentsAsync(inputs, cancellationToken);

            for (var index = 0; index < batch.Length; index++)
            {
                var passage = batch[index];

                var chunk = DocumentChunk.Create(
                    document.TenantId,
                    document.Id,
                    embedded + index,
                    passage.Text,
                    passage.Breadcrumb,
                    passage.PageNumber,
                    passage.StartOffset,
                    passage.EndOffset,
                    passage.ApproximateTokenCount);

                // Recorded per chunk rather than per document, because a document embedded
                // across a provider change would otherwise claim one model for vectors from two.
                chunk.AttachEmbedding(vectors[index], embeddings.ModelIdentity);

                dbContext.DocumentChunks.Add(chunk);
            }

            embedded += batch.Length;
            document.RecordEmbedded(embedded);

            await dbContext.SaveChangesAsync(cancellationToken);

            // A long document can outlive its claim. Renewing between batches keeps another
            // worker from deciding this one died and starting the same document again.
            await renewLease();
        }
    }

    [LoggerMessage(
        EventId = 4200,
        Level = LogLevel.Information,
        Message = "Ingested document {DocumentId} into {ChunkCount} passages.")]
    private static partial void LogIngested(ILogger logger, Guid documentId, int chunkCount);

    [LoggerMessage(
        EventId = 4201,
        Level = LogLevel.Information,
        Message = "Document {DocumentId} was deleted before its ingestion job ran.")]
    private static partial void LogDocumentGone(ILogger logger, Guid documentId);

    [LoggerMessage(
        EventId = 4202,
        Level = LogLevel.Information,
        Message = "Resuming document {DocumentId} at passage {ResumeFrom} of {ChunkCount}.")]
    private static partial void LogResumed(ILogger logger, Guid documentId, int resumeFrom, int chunkCount);
}
