namespace Querio.Domain.Documents;

/// <summary>
/// What a document is, decided by inspecting the bytes rather than trusting the file name.
///
/// Spaced like <see cref="Querio.Domain.Tenants.TenantRole"/> so a format can be inserted
/// later — spreadsheets between documents and plain text, say — without renumbering rows that
/// are already stored.
/// </summary>
public enum FileFormat
{
    PlainText = 10,
    Markdown = 20,
    Pdf = 30,
    Word = 40,
}
