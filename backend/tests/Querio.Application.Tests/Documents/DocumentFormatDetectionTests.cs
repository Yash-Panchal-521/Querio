using System.Text;
using Querio.Application.Documents;
using Querio.Domain.Documents;

namespace Querio.Application.Tests.Documents;

/// <summary>
/// What we accept, and why the name alone is never enough to decide it.
/// </summary>
public sealed class DocumentFormatDetectionTests
{
    [Fact]
    public void A_pdf_is_recognised_from_its_signature_whatever_it_is_called()
    {
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n%âãÏÓ\n1 0 obj");

        DocumentFormatDetection.TryDetect(pdf, "not-a-pdf.txt", out var format).ShouldBeTrue();
        format.ShouldBe(FileFormat.Pdf);
    }

    [Fact]
    public void A_binary_file_dressed_as_text_is_refused()
    {
        // PNG header. The extension says text; the bytes say otherwise, and the bytes win.
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

        DocumentFormatDetection.TryDetect(png, "innocent.txt", out _).ShouldBeFalse();
    }

    [Fact]
    public void A_zip_that_is_not_a_word_document_is_refused()
    {
        // Every .docx is a ZIP — and so is every .xlsx, .pptx and .jar. The signature narrows
        // it to "an archive"; only the name distinguishes the one we can actually read.
        byte[] zip = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00];

        DocumentFormatDetection.TryDetect(zip, "spreadsheet.xlsx", out _).ShouldBeFalse();
        DocumentFormatDetection.TryDetect(zip, "report.docx", out var format).ShouldBeTrue();
        format.ShouldBe(FileFormat.Word);
    }

    [Theory]
    [InlineData("notes.md", FileFormat.Markdown)]
    [InlineData("NOTES.MARKDOWN", FileFormat.Markdown)]
    [InlineData("readme.txt", FileFormat.PlainText)]
    public void Text_is_classified_by_extension_once_the_bytes_agree_it_is_text(
        string fileName,
        FileFormat expected)
    {
        var text = Encoding.UTF8.GetBytes("# Leave policy\n\nParental leave is 26 weeks.");

        DocumentFormatDetection.TryDetect(text, fileName, out var format).ShouldBeTrue();
        format.ShouldBe(expected);
    }

    [Fact]
    public void Text_with_an_extension_we_do_not_accept_is_refused()
    {
        var csv = Encoding.UTF8.GetBytes("name,role\nAda,Owner");

        // Refusing is the point. Treating any text-shaped upload as prose is how a CSV or a
        // source file ends up embedded as if it were documentation, quietly polluting search.
        DocumentFormatDetection.TryDetect(csv, "people.csv", out _).ShouldBeFalse();
    }

    [Fact]
    public void An_empty_prefix_is_not_this_check_s_problem()
    {
        // Nothing in an empty span contradicts "plain text", so this reports plain text. That
        // is correct division of labour rather than a gap: an empty upload is refused by the
        // handler before detection is reached, with a message about the file being empty —
        // which is far more use than "unsupported type".
        DocumentFormatDetection.TryDetect([], "empty.txt", out var format).ShouldBeTrue();
        format.ShouldBe(FileFormat.PlainText);
    }
}
