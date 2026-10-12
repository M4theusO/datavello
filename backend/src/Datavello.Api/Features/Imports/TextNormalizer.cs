using System.Globalization;
using System.Text;

namespace Datavello.Api.Features.Imports;

public static class TextNormalizer
{
    /// <summary>"  Água,   Luz " → "agua, luz": lowercase, no accents, single spaces.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var decomposed = text.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var lastWasSpace = false;
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace)
                {
                    builder.Append(' ');
                }

                lastWasSpace = true;
                continue;
            }

            builder.Append(char.ToLowerInvariant(ch));
            lastWasSpace = false;
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
