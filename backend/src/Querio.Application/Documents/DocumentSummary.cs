using Querio.Domain.Documents;

namespace Querio.Application.Documents;

/// <summary>
/// A document as the interface shows it.
///
/// Carries progress as well as identity, because the list screen asks "is this ready yet" far
/// more often than it asks anything else, and answering from counters costs one row rather
/// than a count over chunks.
/// </summary>
public sealed record DocumentSummary(
    Guid Id,
    string FileName,
    FileFormat Format,
    long ByteSize,
    DocumentStatus Status,
    int ChunkCount,
    int EmbeddedChunkCount,
    string? FailureCode,
    string? FailureReason,
    string? PauseReason,
    DateTimeOffset? ResumesAt,
    Guid UploadedByUserId,
    DateTimeOffset CreatedAt);
