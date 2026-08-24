using System.Text;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Documents;

namespace Querio.Infrastructure.Extraction;

/// <summary>
/// Markdown, read for its headings rather than rendered.
///
/// Only ATX headings — the <c>#</c> form. Setext underlining is rare in documents people
/// upload, and a full parser would be a dependency and a licence to check for a feature worth
/// one method.
///
/// The markers themselves are stripped. What gets stored is what gets embedded and what gets
/// shown in a citation, and "# Handbook" is markup rather than content — it would appear in
/// breadcrumbs, in the chunk inspector, and in whatever a future answer quotes.
/// </summary>
internal sealed class MarkdownTextExtractor : ITextExtractor
{
    private const int MaxHeadingLevel = 6;

    public FileFormat Format => FileFormat.Markdown;

    public async Task<ExtractedText> ExtractAsync(Stream content, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        var source = TextNormalisation.Normalise(await reader.ReadToEndAsync(cancellationToken));

        if (source.Length == 0)
        {
            return ExtractedText.Empty;
        }

        var builder = new StringBuilder(source.Length);
        var blocks = new List<TextBlock>();

        foreach (var paragraph in source.Split("\n\n", StringSplitOptions.None))
        {
            var trimmed = paragraph.Trim();

            if (trimmed.Length == 0)
            {
                continue;
            }

            var level = HeadingLevel(trimmed);
            var text = level is null ? trimmed : trimmed[(level.Value + 1)..].Trim();

            if (text.Length == 0)
            {
                continue;
            }

            var start = builder.Length;
            builder.Append(text);
            blocks.Add(new TextBlock(start, text.Length, level, null));
            builder.Append("\n\n");
        }

        var normalised = builder.ToString().TrimEnd();

        return normalised.Length == 0 ? ExtractedText.Empty : new ExtractedText(normalised, blocks);
    }

    private static int? HeadingLevel(string paragraph)
    {
        var level = 0;

        while (level < paragraph.Length && paragraph[level] == '#')
        {
            level++;
        }

        // A run of hashes with no space after it is not a heading — "#1 priority" is prose.
        return level is > 0 and <= MaxHeadingLevel && level < paragraph.Length && paragraph[level] == ' '
            ? level
            : null;
    }
}
