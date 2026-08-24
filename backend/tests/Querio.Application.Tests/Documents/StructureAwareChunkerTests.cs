using System.Text;
using Querio.Application.Common.Abstractions;
using Querio.Application.Documents.Chunking;
using Querio.Domain.Documents;

namespace Querio.Application.Tests.Documents;

/// <summary>
/// Chunking decides what retrieval can ever return. A boundary in the wrong place cannot be
/// recovered later by a better model or a cleverer query — the passage simply does not contain
/// the answer.
/// </summary>
public sealed class StructureAwareChunkerTests
{
    private readonly StructureAwareChunker chunker = new();

    [Fact]
    public void Short_text_stays_one_passage()
    {
        var extracted = Paragraphs("Parental leave is 26 weeks at full pay.");

        var chunks = chunker.Chunk(extracted);

        chunks.Count.ShouldBe(1);
        chunks[0].Text.ShouldBe("Parental leave is 26 weeks at full pay.");
        chunks[0].Breadcrumb.ShouldBeNull();
        chunks[0].ApproximateTokenCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void A_heading_path_follows_the_passages_beneath_it()
    {
        var extracted = Headed(
            ("Handbook", 1),
            ("Leave", 2),
            ("Parental", 3));

        var chunks = chunker.Chunk(extracted);

        // The body paragraph sits under all three, so it carries the whole path — which is what
        // turns a retrieved fragment into something a reader can place.
        chunks[^1].Breadcrumb.ShouldBe("Handbook › Leave › Parental");
    }

    [Fact]
    public void A_sibling_heading_replaces_its_predecessor_rather_than_nesting_under_it()
    {
        var text =
            "# Handbook\n\n## Leave\n\nParental leave is 26 weeks.\n\n## Benefits\n\nDental cover is included.";

        var extracted = Markdown(text);
        var chunks = chunker.Chunk(extracted);

        var benefits = chunks.Last(chunk => chunk.Text.Contains("Dental", StringComparison.Ordinal));

        // "Handbook › Leave › Benefits" would be wrong and would mislead every citation under
        // it. A heading closes every heading at or below its own level.
        benefits.Breadcrumb.ShouldBe("Handbook › Benefits");
    }

    [Fact]
    public void Long_text_splits_with_overlap_so_a_straddling_sentence_survives_somewhere()
    {
        var paragraph = string.Join(" ", Enumerable.Range(0, 400).Select(index => $"Sentence number {index}."));
        var extracted = Paragraphs(paragraph);

        var chunks = chunker.Chunk(extracted);

        chunks.Count.ShouldBeGreaterThan(1);
        chunks.ShouldAllBe(chunk => chunk.Text.Length <= ChunkingLimits.MaxChunkCharacters);

        // Consecutive passages must share text, or a question whose answer spans the boundary
        // is answerable from neither.
        var first = chunks[0];
        var second = chunks[1];

        second.StartOffset.ShouldBeLessThan(first.EndOffset);
    }

    [Fact]
    public void Offsets_point_at_the_text_the_passage_came_from()
    {
        var paragraph = string.Join(" ", Enumerable.Range(0, 300).Select(index => $"Clause {index} applies."));
        var extracted = Paragraphs(paragraph);

        var chunks = chunker.Chunk(extracted);

        // Offsets are what a citation highlights. If they drift, the interface underlines the
        // wrong sentence and quietly undermines every answer.
        foreach (var chunk in chunks)
        {
            var slice = extracted.Text[chunk.StartOffset..chunk.EndOffset];
            slice.Trim().ShouldBe(chunk.Text);
        }
    }

    [Fact]
    public void An_unbroken_run_longer_than_a_passage_is_still_split()
    {
        // No spaces, no sentence ends, no paragraph breaks — a minified file, or a language
        // this splitter does not understand. The requirement is that it terminates and stays
        // within the limit, not that the result reads well.
        var extracted = Paragraphs(new string('x', ChunkingLimits.MaxChunkCharacters * 3));

        var chunks = chunker.Chunk(extracted);

        chunks.Count.ShouldBeGreaterThan(1);
        chunks.ShouldAllBe(chunk => chunk.Text.Length <= ChunkingLimits.MaxChunkCharacters);
    }

    [Fact]
    public void Empty_text_produces_no_passages_rather_than_an_empty_one()
    {
        chunker.Chunk(ExtractedText.Empty).ShouldBeEmpty();
        chunker.Chunk(new ExtractedText("   \n\n  ", [])).ShouldBeEmpty();
    }

    private static ExtractedText Paragraphs(string text) =>
        new(text, [new TextBlock(0, text.Length, null, null)]);

    /// <summary>
    /// Mirrors what MarkdownTextExtractor produces, markers stripped and all. Application
    /// cannot reference Infrastructure, so this stands in — and it has to stand in accurately,
    /// or these tests pass against text no extractor ever emits.
    /// </summary>
    private static ExtractedText Markdown(string source)
    {
        var builder = new StringBuilder();
        var blocks = new List<TextBlock>();

        foreach (var paragraph in TextNormalisation.Normalise(source).Split("\n\n"))
        {
            var trimmed = paragraph.Trim();

            if (trimmed.Length == 0)
            {
                continue;
            }

            var hashes = trimmed.TakeWhile(character => character == '#').Count();
            var level = hashes is > 0 and <= 6 && hashes < trimmed.Length && trimmed[hashes] == ' '
                ? hashes
                : (int?)null;

            var text = level is null ? trimmed : trimmed[(level.Value + 1)..].Trim();

            var start = builder.Length;
            builder.Append(text);
            blocks.Add(new TextBlock(start, text.Length, level, null));
            builder.Append("\n\n");
        }

        return new ExtractedText(builder.ToString().TrimEnd(), blocks);
    }

    private static ExtractedText Headed(params (string Title, int Level)[] headings)
    {
        var source = string.Join(
            "\n\n",
            headings.Select(heading => $"{new string('#', heading.Level)} {heading.Title}")
                .Append("The body paragraph that sits beneath every one of them."));

        return Markdown(source);
    }
}
