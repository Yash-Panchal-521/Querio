using System.Text;

namespace Querio.Application.Common.Abstractions;

/// <summary>
/// The shape every extractor has to produce.
///
/// Public because the implementations live in Infrastructure: <see cref="ExtractedText"/>
/// promises normalised text, and a promise the implementers cannot reach is not a contract.
/// </summary>
public static class TextNormalisation
{
    /// <summary>
    /// Collapses line endings and runs of blank lines, and trims the ends.
    ///
    /// This is what makes a blank line mean "new paragraph" reliably. A PDF breaks every
    /// visual line, so without it the text would be a stack of one-line paragraphs and the
    /// chunker would have nothing meaningful to break on.
    /// </summary>
    public static string Normalise(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var builder = new StringBuilder(text.Length);
        var consecutiveNewlines = 0;

        foreach (var character in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'))
        {
            if (character == '\n')
            {
                consecutiveNewlines++;

                // Two is a paragraph break; more is just whitespace nobody meant.
                if (consecutiveNewlines > 2)
                {
                    continue;
                }
            }
            else
            {
                consecutiveNewlines = 0;
            }

            builder.Append(character);
        }

        return builder.ToString().Trim();
    }
}
