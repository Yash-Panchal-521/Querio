using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Querio.Application.Common.Abstractions;
using Querio.Application.Documents.Chunking;
using Querio.Domain.Documents;
using Querio.Infrastructure.Extraction;
using WordDocument = DocumentFormat.OpenXml.Wordprocessing.Document;

namespace Querio.Api.Tests.Extraction;

/// <summary>
/// Extraction plus chunking, together. Testing them apart would prove each half works and
/// still leave the thing that matters unproven: whether a real file ends up as passages a
/// citation can point at.
/// </summary>
public sealed class TextExtractorTests
{
    private readonly StructureAwareChunker chunker = new();

    [Fact]
    public async Task Markdown_headings_survive_as_breadcrumbs_without_their_markers()
    {
        const string source = """
            # Employee handbook

            ## Leave

            ### Parental

            Parental leave is 26 weeks at full pay.

            ## Benefits

            Dental cover is included for partners.
            """;

        var chunks = await ChunkAsync(new MarkdownTextExtractor(), source);

        var parental = chunks.Single(chunk => chunk.Text.Contains("26 weeks", StringComparison.Ordinal));
        parental.Breadcrumb.ShouldBe("Employee handbook › Leave › Parental");

        // A sibling heading replaces its predecessor rather than nesting beneath it.
        var dental = chunks.Single(chunk => chunk.Text.Contains("Dental", StringComparison.Ordinal));
        dental.Breadcrumb.ShouldBe("Employee handbook › Benefits");

        // The hashes are markup. Leaving them in would put them in the breadcrumb, in the
        // chunk inspector, and in whatever an answer eventually quotes.
        chunks.ShouldAllBe(chunk => !chunk.Text.StartsWith('#'));
    }

    [Fact]
    public async Task Word_heading_styles_are_read_as_structure()
    {
        using var docx = BuildWordDocument(
            ("Employee handbook", 1),
            ("Leave", 2),
            (null, 0),
            ("Benefits", 2));

        var chunks = await ChunkAsync(new WordTextExtractor(), docx);

        // This is the one format where headings are stated rather than inferred: a paragraph
        // styled Heading2 is a second-level heading because its author said so.
        var body = chunks.Single(chunk => chunk.Text.Contains("body paragraph", StringComparison.Ordinal));
        body.Breadcrumb.ShouldBe("Employee handbook › Leave");
    }

    [Fact]
    public async Task A_file_that_is_not_really_a_pdf_fails_with_something_a_person_can_act_on()
    {
        using var nonsense = new MemoryStream("%PDF-1.4 and then absolute rubbish"u8.ToArray());

        var failure = await Should.ThrowAsync<DocumentExtractionException>(
            async () => await new PdfTextExtractor().ExtractAsync(nonsense, TestContext.Current.CancellationToken));

        failure.FailureCode.ShouldBe(DocumentExtractionException.Unreadable);

        // Shown to whoever uploaded it, so it has to read as a sentence rather than as a stack
        // trace from inside a parser.
        failure.Message.ShouldContain("could not be read");
    }

    [Fact]
    public async Task Windows_line_endings_do_not_become_paragraph_breaks()
    {
        // A file saved on Windows has CRLF everywhere. Left alone, every line would read as a
        // paragraph and the chunker would have a boundary after each one.
        var source = "First line.\r\nSecond line.\r\n\r\nA new paragraph.";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(source));
        var extracted = await new PlainTextExtractor().ExtractAsync(stream, TestContext.Current.CancellationToken);

        extracted.Text.ShouldNotContain("\r");
        extracted.Blocks.Count.ShouldBe(2);
    }

    private async Task<IReadOnlyList<TextChunk>> ChunkAsync(ITextExtractor extractor, string source)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(source));

        return await ChunkAsync(extractor, stream);
    }

    private async Task<IReadOnlyList<TextChunk>> ChunkAsync(ITextExtractor extractor, Stream source)
    {
        var extracted = await extractor.ExtractAsync(source, TestContext.Current.CancellationToken);

        return chunker.Chunk(extracted);
    }

    /// <summary>
    /// A real .docx, built with the same library that reads it. A fixture file checked into the
    /// repository would work too, but this shows exactly what the test depends on.
    /// </summary>
    private static MemoryStream BuildWordDocument(params (string? Heading, int Level)[] paragraphs)
    {
        var stream = new MemoryStream();

        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, autoSave: true))
        {
            var body = new Body();

            foreach (var (heading, level) in paragraphs)
            {
                body.Append(heading is null
                    ? new Paragraph(new Run(new Text("The body paragraph beneath them.")))
                    : new Paragraph(
                        new ParagraphProperties(new ParagraphStyleId { Val = $"Heading{level}" }),
                        new Run(new Text(heading))));
            }

            document.AddMainDocumentPart().Document = new WordDocument(body);
        }

        stream.Position = 0;

        return stream;
    }
}
