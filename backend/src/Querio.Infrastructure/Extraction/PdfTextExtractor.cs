using System.Text;
using Querio.Application.Common.Abstractions;
using Querio.Domain.Documents;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace Querio.Infrastructure.Extraction;

/// <summary>
/// PDF text, page by page.
///
/// No headings: a PDF's structure is visual, not semantic. What looks like a heading is text
/// that happens to be larger, and inferring from font size is guesswork that gets a document's
/// hierarchy confidently wrong. Pages are recorded instead, which is what a citation needs
/// anyway — "page 12" is more use to a reader than a section name we guessed.
/// </summary>
internal sealed class PdfTextExtractor : ITextExtractor
{
    public FileFormat Format => FileFormat.Pdf;

    public Task<ExtractedText> ExtractAsync(Stream content, CancellationToken cancellationToken)
    {
        PdfDocument document;

        try
        {
            document = PdfDocument.Open(content);
        }
        catch (PdfDocumentEncryptedException)
        {
            throw new DocumentExtractionException(
                DocumentExtractionException.Encrypted,
                "This PDF is password-protected. Remove the password and upload it again.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new DocumentExtractionException(
                DocumentExtractionException.Unreadable,
                "This PDF could not be read. It may be damaged or incomplete.");
        }

        using (document)
        {
            var builder = new StringBuilder();
            var blocks = new List<TextBlock>();

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Content order rather than raw order: PDFs store text in whatever sequence the
                // producer emitted it, which for multi-column layouts interleaves the columns.
                var pageText = TextNormalisation.Normalise(ContentOrderTextExtractor.GetText(page));

                if (pageText.Length == 0)
                {
                    continue;
                }

                var start = builder.Length;
                builder.Append(pageText);
                blocks.Add(new TextBlock(start, pageText.Length, null, page.Number));
                builder.Append("\n\n");
            }

            var text = builder.ToString().TrimEnd();

            // Structurally valid and containing nothing to embed — a scan. Reported as its own
            // failure so the uploader is told their document is images, rather than left with a
            // document that succeeded and answers nothing.
            return Task.FromResult(text.Length == 0 ? ExtractedText.Empty : new ExtractedText(text, blocks));
        }
    }
}
