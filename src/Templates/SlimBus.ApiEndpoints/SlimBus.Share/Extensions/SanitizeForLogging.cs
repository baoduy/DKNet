namespace SlimBus.Share.Extensions;

/// <summary>
///     Provides extension methods for sanitizing strings for logging purposes.
/// </summary>
public static class SanitizeForLoggingExtensions
{
    #region Methods

    /// <summary>
    ///     Sanitizes a string value by removing control characters and whitespace that could interfere with logging.
    /// </summary>
    /// <param name="value">The string value to sanitize.</param>
    /// <returns>A sanitized string safe for logging.</returns>
    public static string SanitizeForLogging(this string value) =>
        value.Replace("\n", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("\t", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim() // Trim leading and trailing spaces
            .Replace('\0', ' ') // Replace null characters
            .Replace('\f', ' ') // Replace form feed characters
            .Replace('\r', ' ');

    #endregion

    // Sanitize user input
}