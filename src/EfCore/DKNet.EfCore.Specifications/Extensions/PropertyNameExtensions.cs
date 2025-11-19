using System.Text;

namespace DKNet.EfCore.Specifications.Extensions;

/// <summary>
///     Extension methods for normalizing property path strings (camelCase, snake_case, kebab-case) into
///     PascalCase segments suitable for C# / EF Core member access.
/// </summary>
public static class PropertyNameExtensions
{
    #region Fields

    /// <summary>
    ///     Characters that are treated as word boundaries when converting to PascalCase.
    ///     Includes underscore ('_') and hyphen ('-').
    /// </summary>
    private static readonly char[] SegmentSeparators = ['_', '-'];

    #endregion

    #region Methods

    /// <summary>
    ///     Converts a string to PascalCase. If the string contains dot separators ('.'),
    ///     each segment is normalized to PascalCase and segments are joined with dots.
    ///     Otherwise, the string is treated as a single segment and normalized to PascalCase.
    ///     Word boundaries are detected at underscores ('_') and hyphens ('-').
    ///     Existing inner casing is preserved (e.g. lineITEM => LineITEM).
    /// </summary>
    /// <param name="propertyPath">The raw segment or path to convert.</param>
    /// <returns>
    ///     PascalCase string or PascalCase path. Returns an empty string if <paramref name="propertyPath" /> is null or empty.
    /// </returns>
    public static string ToPascalCase(this string? propertyPath)
    {
        if (string.IsNullOrWhiteSpace(propertyPath)) return string.Empty;
        return propertyPath.Contains('.', StringComparison.OrdinalIgnoreCase)
            ? ToPascalCasePathInternal(propertyPath)
            : ToPascalCaseInternal(propertyPath);
    }

    /// <summary>
    ///     Converts a single path segment to PascalCase. Splits on '_' and '-' treating them as word boundaries.
    ///     The first character of each word is capitalized, and the rest of the word is preserved as-is.
    ///     Example: "foo_bar-baz" => "FooBarBaz".
    /// </summary>
    /// <param name="name">Raw segment text.</param>
    /// <returns>PascalCase segment; empty string for null/whitespace.</returns>
    private static string ToPascalCaseInternal(string name)
    {
        var parts = name.Split(SegmentSeparators, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            sb.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1) sb.Append(part.AsSpan(1));
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Converts a dotted property path into PascalCase for each segment.
    ///     Each segment is split on '_' and '-' and normalized to PascalCase.
    ///     Example: "user_profile.address_city" => "UserProfile.AddressCity".
    /// </summary>
    /// <param name="path">Raw property path; segments may contain '-', '_' or mixed casing.</param>
    /// <returns>
    ///     Normalized PascalCase property path. Returns empty string if <paramref name="path" /> is null or whitespace.
    /// </returns>
    private static string ToPascalCasePathInternal(this string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
            segments[i] = ToPascalCaseInternal(segments[i]);
        return string.Join('.', segments);
    }

    #endregion
}