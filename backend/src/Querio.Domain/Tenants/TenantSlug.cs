using System.Globalization;
using System.Text;

namespace Querio.Domain.Tenants;

/// <summary>
/// Derives a readable URL slug from an organization name, so nobody is ever asked to invent
/// a unique one themselves.
/// </summary>
public static class TenantSlug
{
    private const int MaxLength = 48;
    private const string Fallback = "org";


    public static string From(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        // Strip accents first, so "Café Ltd" becomes "cafe-ltd" rather than losing the word.
        var decomposed = name.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var lastWasSeparator = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) is UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator && builder.Length > 0)
            {
                // Collapse any run of punctuation or whitespace into a single hyphen.
                builder.Append('-');
                lastWasSeparator = true;
            }
        }

        var slug = builder.ToString().Trim('-');

        if (slug.Length > MaxLength)
        {
            slug = slug[..MaxLength].TrimEnd('-');
        }

        // A name of only symbols — "!!!" — would otherwise produce an empty slug.
        return slug.Length == 0 ? Fallback : slug;
    }

    /// <summary>Appends the suffix used when the preferred slug is already taken.</summary>
    public static string WithSuffix(string slug, int suffix)
    {
        var suffixText = $"-{suffix.ToString(CultureInfo.InvariantCulture)}";
        var room = MaxLength - suffixText.Length;
        var stem = slug.Length > room ? slug[..room].TrimEnd('-') : slug;

        return stem + suffixText;
    }
}
