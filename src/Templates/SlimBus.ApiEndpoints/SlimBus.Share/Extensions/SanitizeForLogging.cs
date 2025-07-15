namespace SlimBus.Share.Extensions;

public static class SanitizeForLoggingExtensions
{
    public static string SanitizeForLogging(this string value) =>
        value.Replace("\n", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("\t", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim() // Trim leading and trailing spaces
            .Replace('\0', ' ') // Replace null characters
            .Replace('\f', ' ') // Replace form feed characters
            .Replace('\r', ' ');
    // Sanitize user input
}