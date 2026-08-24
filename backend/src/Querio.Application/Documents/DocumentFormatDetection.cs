using Querio.Domain.Documents;

namespace Querio.Application.Documents;

/// <summary>
/// Decides what an upload actually is.
///
/// From the bytes first, and only then from the name. A file called <c>report.pdf</c> that is
/// really a video would otherwise reach the extractor, fail somewhere deep inside a parser,
/// and be reported to the uploader as an internal error rather than as the wrong file.
///
/// Extensions still matter for the formats that share a container: every <c>.docx</c> is a ZIP
/// archive, and so is every <c>.xlsx</c>, <c>.pptx</c> and <c>.jar</c>. The magic bytes narrow
/// it to "a ZIP"; the name is what distinguishes the one we can read.
/// </summary>
internal static class DocumentFormatDetection
{
    /// <summary>Enough to cover every signature below and to sample for binary content.</summary>
    public const int PrefixBytes = 512;

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    public static bool TryDetect(ReadOnlySpan<byte> prefix, string fileName, out FileFormat format)
    {
        var extension = Path.GetExtension(fileName);

        if (prefix.StartsWith(PdfSignature))
        {
            format = FileFormat.Pdf;
            return true;
        }

        if (prefix.StartsWith(ZipSignature))
        {
            format = FileFormat.Word;
            return extension.Equals(".docx", StringComparison.OrdinalIgnoreCase);
        }

        // A NUL byte in the first half-kilobyte means this is not text, whatever it is called.
        // Real prose does not contain one, and every binary format we might be handed does.
        if (prefix.Contains((byte)0))
        {
            format = default;
            return false;
        }

        if (extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase))
        {
            format = FileFormat.Markdown;
            return true;
        }

        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".text", StringComparison.OrdinalIgnoreCase))
        {
            format = FileFormat.PlainText;
            return true;
        }

        // Text-shaped but not a name we accept. Refusing is deliberate: silently treating an
        // unknown extension as plain text is how a source file or a CSV ends up embedded as
        // prose and quietly poisons search results.
        format = default;
        return false;
    }
}
