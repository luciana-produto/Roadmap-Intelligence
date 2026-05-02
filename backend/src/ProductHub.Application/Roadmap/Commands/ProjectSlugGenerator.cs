using System.Text;
using System.Text.RegularExpressions;

namespace ProductHub.Application.Roadmap.Commands;

internal static partial class ProjectSlugGenerator
{
    public static string Generate(string name)
    {
        var normalized = name.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        var slug = builder
            .ToString()
            .ToLowerInvariant();

        slug = NonAlphaNumericRegex().Replace(slug, "-").Trim('-');
        slug = RepeatedDashRegex().Replace(slug, "-");

        return slug.Length == 0 ? "projeto" : slug;
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex NonAlphaNumericRegex();

    [GeneratedRegex("-{2,}", RegexOptions.Compiled)]
    private static partial Regex RepeatedDashRegex();
}