using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Documents;

namespace Querio.Infrastructure.Extraction;

/// <summary>
/// Word documents, read through their styles.
///
/// This is the format where headings are genuinely knowable rather than inferred: a paragraph
/// styled Heading2 <em>is</em> a second-level heading, because someone said so. That makes the
/// breadcrumbs on a Word document's chunks the most trustworthy of any format we accept.
/// </summary>
internal sealed class WordTextExtractor : ITextExtractor
{
    private const string HeadingStylePrefix = "Heading";
    private const int MaxHeadingLevel = 6;

    public FileFormat Format => FileFormat.Word;

    public Task<ExtractedText> ExtractAsync(Stream content, CancellationToken cancellationToken)
    {
        WordprocessingDocument document;

        try
        {
            document = WordprocessingDocument.Open(content, isEditable: false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new DocumentExtractionException(
                DocumentExtractionException.Unreadable,
                "This Word document could not be read. It may be damaged, or saved in an older format.");
        }

        using (document)
        {
            var body = document.MainDocumentPart?.Document?.Body;

            if (body is null)
            {
                return Task.FromResult(ExtractedText.Empty);
            }

            var builder = new StringBuilder();
            var blocks = new List<TextBlock>();

            foreach (var paragraph in body.Descendants<Paragraph>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var text = paragraph.InnerText.Trim();

                if (text.Length == 0)
                {
                    continue;
                }

                var start = builder.Length;
                builder.Append(text);
                blocks.Add(new TextBlock(start, text.Length, HeadingLevel(paragraph), null));
                builder.Append("\n\n");
            }

            var normalised = builder.ToString().TrimEnd();

            return Task.FromResult(
                normalised.Length == 0 ? ExtractedText.Empty : new ExtractedText(normalised, blocks));
        }
    }

    /// <summary>
    /// Style ids are "Heading1" through "Heading9" in English builds. Anything outside 1–6 is
    /// treated as body text, matching what the chunker's breadcrumb depth can express.
    /// </summary>
    private static int? HeadingLevel(Paragraph paragraph)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;

        if (styleId is null || !styleId.StartsWith(HeadingStylePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return int.TryParse(
            styleId.AsSpan(HeadingStylePrefix.Length),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var level) && level is > 0 and <= MaxHeadingLevel
            ? level
            : null;
    }
}
