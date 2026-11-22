using System.Globalization;
using System.Text;

namespace Bravo.Application.Imports.Tse.Services;

/// <summary>
/// Normalises municipality names for comparison against IBGE names.
/// </summary>
/// <remarks>
/// TSE and IBGE spellings differ in diacritics, punctuation and abbreviations. Normalisation
/// is deliberately conservative: it only removes accent and case differences and collapses
/// whitespace, so genuinely different names never collide.
/// </remarks>
public static class MunicipalityNameNormaliser
{
    public static string Normalise(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToUpperInvariant(character));
            else
                builder.Append(' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
