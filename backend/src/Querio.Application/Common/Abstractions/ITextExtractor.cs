using Querio.Domain.Documents;

namespace Querio.Application.Common.Abstractions;

/// <summary>
/// Turns one file format into text plus enough structure to cite it later.
///
/// One implementation per format, selected by <see cref="Format"/>. Extraction is where a file
/// stops being a PDF or a Word document and becomes the same shape as every other — everything
/// downstream works on <see cref="ExtractedText"/> and never learns what it came from.
/// </summary>
public interface ITextExtractor
{
    FileFormat Format { get; }

    Task<ExtractedText> ExtractAsync(Stream content, CancellationToken cancellationToken);
}

/// <summary>
/// The whole document as one string, with blocks pointing into it.
///
/// One string rather than a list of paragraphs, because chunk boundaries rarely fall on
/// paragraph boundaries and the offsets have to mean something in the text a citation will
/// eventually highlight. Blocks index into it rather than copying it.
/// </summary>
/// <param name="Text">Normalised text: CRLF collapsed, no more than one blank line in a row.</param>
/// <param name="Blocks">In document order, non-overlapping.</param>
public sealed record ExtractedText(string Text, IReadOnlyList<TextBlock> Blocks)
{
    public static ExtractedText Empty { get; } = new(string.Empty, []);

    /// <summary>
    /// True when extraction produced nothing worth embedding. The usual cause is a scanned
    /// PDF: structurally valid, full of images, and containing no text at all.
    /// </summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Text);
}

/// <param name="HeadingLevel">1–6 for a heading, null for body text.</param>
/// <param name="PageNumber">Set only by formats that have pages.</param>
public sealed record TextBlock(int StartOffset, int Length, int? HeadingLevel, int? PageNumber)
{
    public int EndOffset => StartOffset + Length;

    public bool IsHeading => HeadingLevel is not null;
}
