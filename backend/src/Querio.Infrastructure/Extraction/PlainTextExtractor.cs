using System.Text;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Documents;

namespace Querio.Infrastructure.Extraction;

/// <summary>
/// Plain text: paragraphs, no structure to read.
/// </summary>
internal sealed class PlainTextExtractor : ITextExtractor
{
    public FileFormat Format => FileFormat.PlainText;

    public async Task<ExtractedText> ExtractAsync(Stream content, CancellationToken cancellationToken)
    {
        // detectEncodingFromByteOrderMarks, so a file saved as UTF-16 by a Windows editor does
        // not arrive as text interleaved with NUL bytes.
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        return TextBlockBuilder.FromParagraphs(await reader.ReadToEndAsync(cancellationToken));
    }
}
