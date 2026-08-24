namespace Querio.Application.Common.Abstractions;

/// <summary>
/// Splits extracted text into the passages that get embedded and, later, cited.
/// </summary>
public interface IChunker
{
    IReadOnlyList<TextChunk> Chunk(ExtractedText extracted);
}

/// <param name="Breadcrumb">
/// The heading path above this passage — "Handbook › Leave › Parental". Null where the format
/// carries no headings to read.
/// </param>
/// <param name="ApproximateTokenCount">Indicative. See <c>ChunkingLimits</c>.</param>
public sealed record TextChunk(
    string Text,
    string? Breadcrumb,
    int? PageNumber,
    int StartOffset,
    int EndOffset,
    int ApproximateTokenCount);
