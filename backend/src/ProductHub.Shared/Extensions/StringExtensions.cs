namespace ProductHub.Shared.Extensions;

public static class StringExtensions
{
    public static bool IsNullOrWhiteSpace(this string? value) =>
        string.IsNullOrWhiteSpace(value);

    public static string ToSnakeCase(this string value) =>
        string.IsNullOrEmpty(value)
            ? value
            : System.Text.RegularExpressions.Regex
                .Replace(value, "([a-z])([A-Z])", "$1_$2")
                .ToLowerInvariant();

    public static string Truncate(this string value, int maxLength, string suffix = "...") =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - suffix.Length), suffix);
}
