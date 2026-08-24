using Querio.Application.Common.Abstractions;
using Querio.Domain.Documents;

namespace Querio.Application.Documents.Chunking;

/// <summary>
/// Packs extracted text into passages, breaking where the document itself breaks.
///
/// The naive version — cut every N characters — is why so many retrieval systems answer with
/// half a sentence from the middle of an unrelated section. This prefers a block boundary,
/// falls back to a sentence end, then to a word, and only cuts mid-word when a single
/// unbroken run is longer than a whole passage.
///
/// Each passage keeps the heading path above it, so a retrieved fragment arrives as
/// "Handbook › Leave › Parental" rather than as an anonymous paragraph.
/// </summary>
internal sealed class StructureAwareChunker : IChunker
{
    private static readonly char[] SentenceEndings = ['.', '!', '?'];

    public IReadOnlyList<TextChunk> Chunk(ExtractedText extracted)
    {
        ArgumentNullException.ThrowIfNull(extracted);

        if (extracted.IsEmpty)
        {
            return [];
        }

        var text = extracted.Text;
        var breadcrumbs = BuildBreadcrumbs(extracted);
        var chunks = new List<TextChunk>();

        var position = SkipWhitespace(text, 0);

        while (position < text.Length)
        {
            var end = ChooseEnd(text, extracted, position);
            var body = text[position..end].Trim();

            if (body.Length > 0)
            {
                chunks.Add(new TextChunk(
                    body,
                    // Read at the end, not the start. A passage that opens with a run of
                    // headings — "Handbook", "Leave", "Parental" — sits under the deepest of
                    // them, and labelling it with the first would misplace every citation.
                    BreadcrumbAt(breadcrumbs, end - 1),
                    PageAt(extracted, position),
                    position,
                    end,
                    Math.Max(1, body.Length / ChunkingLimits.CharactersPerToken)));
            }

            if (end >= text.Length)
            {
                break;
            }

            // Step back by the overlap so a sentence straddling the boundary survives whole in
            // the next passage — but never so far that we fail to advance, which would loop.
            //
            // Not across a section boundary, though. Overlapping there would open the next
            // section's passage with the tail of the previous one, giving text one breadcrumb
            // while it belongs under another — and duplicating it into two passages that then
            // compete in search results.
            var next = StartsASection(extracted, end)
                ? end
                : SkipWhitespace(text, Math.Max(position + 1, end - ChunkingLimits.OverlapCharacters));

            position = next <= position ? SkipWhitespace(text, end) : next;
        }

        return chunks;
    }

    /// <summary>
    /// Where this passage should stop: the last block boundary that fits, else the last
    /// sentence end, else the last word break, else the hard limit.
    /// </summary>
    private static int ChooseEnd(string text, ExtractedText extracted, int position)
    {
        // A passage never crosses into a new section. Letting it would give the combined chunk
        // one breadcrumb for content belonging to two, which is worse than a shorter passage.
        var hardStop = NextSectionStart(extracted, position) ?? text.Length;
        var limit = Math.Min(position + ChunkingLimits.MaxChunkCharacters, hardStop);

        if (limit >= hardStop)
        {
            return hardStop;
        }

        // A remainder too small to stand on its own is absorbed here rather than left to
        // become a passage that matches everything and means nothing.
        if (hardStop - limit < ChunkingLimits.MinChunkCharacters)
        {
            return hardStop;
        }

        var blockBoundary = LastBlockBoundary(extracted, position, limit);

        if (blockBoundary > position + ChunkingLimits.MinChunkCharacters)
        {
            return blockBoundary;
        }

        var sentenceEnd = LastSentenceEnd(text, position, limit);

        if (sentenceEnd > position + ChunkingLimits.MinChunkCharacters)
        {
            return sentenceEnd;
        }

        var wordBreak = text.LastIndexOf(' ', limit - 1, limit - position - 1);

        // Only reached by an unbroken run longer than a whole passage — a minified file, or a
        // language this splitter does not understand. Cutting mid-word beats not cutting.
        return wordBreak > position ? wordBreak : limit;
    }

    /// <summary>
    /// Where the next section begins: the first heading that follows actual content.
    ///
    /// "Follows content" is the important half. Documents routinely open with several headings
    /// in a row — a title, then a section, then a subsection — and breaking between them would
    /// emit passages consisting of nothing but a heading, which embed to vectors that match
    /// almost anything.
    /// </summary>
    private static int? NextSectionStart(ExtractedText extracted, int position)
    {
        var seenContent = false;

        foreach (var block in extracted.Blocks)
        {
            if (block.EndOffset <= position)
            {
                continue;
            }

            if (!block.IsHeading)
            {
                seenContent = true;
                continue;
            }

            if (seenContent && block.StartOffset > position)
            {
                return block.StartOffset;
            }
        }

        return null;
    }

    private static bool StartsASection(ExtractedText extracted, int offset) =>
        extracted.Blocks.Any(block => block.IsHeading && block.StartOffset == offset);

    private static int LastBlockBoundary(ExtractedText extracted, int position, int limit)
    {
        var boundary = -1;

        foreach (var block in extracted.Blocks)
        {
            if (block.StartOffset > position && block.StartOffset <= limit)
            {
                boundary = block.StartOffset;
            }
            else if (block.StartOffset > limit)
            {
                break;
            }
        }

        return boundary;
    }

    private static int LastSentenceEnd(string text, int position, int limit)
    {
        for (var index = limit - 1; index > position; index--)
        {
            if (Array.IndexOf(SentenceEndings, text[index]) < 0)
            {
                continue;
            }

            // Followed by whitespace, so "3.5" and "Ms." do not read as sentence ends.
            if (index + 1 < text.Length && !char.IsWhiteSpace(text[index + 1]))
            {
                continue;
            }

            return index + 1;
        }

        return -1;
    }

    /// <summary>
    /// Walks the headings once, recording the full path in force from each offset onward, so
    /// looking one up per passage is a scan of a small list rather than a re-walk.
    /// </summary>
    private static List<(int Offset, string Breadcrumb)> BuildBreadcrumbs(ExtractedText extracted)
    {
        var breadcrumbs = new List<(int, string)>();
        var stack = new List<(int Level, string Title)>();

        foreach (var block in extracted.Blocks.Where(block => block.IsHeading))
        {
            var title = extracted.Text
                .Substring(block.StartOffset, block.Length)
                .Trim();

            if (title.Length == 0)
            {
                continue;
            }

            // A heading closes every heading at or below its own level, which is what makes
            // "Leave › Parental" become "Benefits" when the next H1 arrives.
            stack.RemoveAll(entry => entry.Level >= block.HeadingLevel);
            stack.Add((block.HeadingLevel!.Value, title));

            var path = string.Join(" › ", stack.Select(entry => entry.Title));

            breadcrumbs.Add((
                block.StartOffset,
                path.Length <= DocumentChunk.MaxBreadcrumbLength
                    ? path
                    : path[^DocumentChunk.MaxBreadcrumbLength..]));
        }

        return breadcrumbs;
    }

    private static string? BreadcrumbAt(List<(int Offset, string Breadcrumb)> breadcrumbs, int position)
    {
        string? found = null;

        foreach (var (offset, breadcrumb) in breadcrumbs)
        {
            if (offset > position)
            {
                break;
            }

            found = breadcrumb;
        }

        return found;
    }

    private static int? PageAt(ExtractedText extracted, int position)
    {
        int? page = null;

        foreach (var block in extracted.Blocks)
        {
            if (block.StartOffset > position)
            {
                break;
            }

            page = block.PageNumber ?? page;
        }

        return page;
    }

    private static int SkipWhitespace(string text, int position)
    {
        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position++;
        }

        return position;
    }
}
