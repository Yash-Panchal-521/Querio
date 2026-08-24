using Querio.Application.Common.Abstractions;


namespace Querio.Infrastructure.Extraction;

/// <summary>
/// Assembles normalised text and the blocks pointing into it, so each extractor only has to
/// decide what a paragraph or a heading is in its own format.
/// </summary>
internal static class TextBlockBuilder
{
    /// <summary>
    /// Splits already-normalised text on blank lines, optionally recognising headings.
    ///
    /// Blank lines rather than single newlines because a PDF breaks every visual line, and
    /// treating those as paragraph boundaries would produce a block per line and a chunker
    /// with nothing useful to break on.
    /// </summary>
    public static ExtractedText FromParagraphs(string rawText, Func<string, int?>? headingLevel = null)
    {
        var text = TextNormalisation.Normalise(rawText);

        if (string.IsNullOrWhiteSpace(text))
        {
            return ExtractedText.Empty;
        }

        var blocks = new List<TextBlock>();
        var position = 0;

        while (position < text.Length)
        {
            var separator = text.IndexOf("\n\n", position, StringComparison.Ordinal);
            var end = separator < 0 ? text.Length : separator;
            var paragraph = text[position..end];

            if (!string.IsNullOrWhiteSpace(paragraph))
            {
                blocks.Add(new TextBlock(position, paragraph.Length, headingLevel?.Invoke(paragraph), null));
            }

            position = separator < 0 ? text.Length : separator + 2;
        }

        return new ExtractedText(text, blocks);
    }
}
