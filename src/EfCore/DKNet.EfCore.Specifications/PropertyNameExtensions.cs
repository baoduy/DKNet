using System.Text;

namespace DKNet.EfCore.Specifications;

/// <summary>
///     Extension methods for normalizing property path strings (camelCase, snake_case, kebab-case) into
///     PascalCase segments suitable for C# / EF Core member access.
/// </summary>
public static class PropertyNameExtensions
{
    #region Fields

    private static readonly char[] SegmentSeparators = ['_', '-'];

    #endregion

    #region Methods

    /// <summary>
    ///     Converts a single path segment to PascalCase. Splits on '_' and '-' treating them as word boundaries.
    ///     Existing inner casing is preserved (e.g. lineITEM => LineITEM).
    /// </summary>
    /// <param name="segment">Raw segment text.</param>
    /// <returns>PascalCase segment; empty string for null/whitespace.</returns>
    public static string ToPascalCase(this string? segment)
        => string.IsNullOrWhiteSpace(segment) ? string.Empty : ToPascalCaseInternal(segment!);

    private static string ToPascalCaseInternal(string segment)
    {
        var parts = segment.Split(SegmentSeparators, StringSplitOptions.RemoveEmptyEntries);
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
    ///     Example: "user_profile.address_city" => "UserProfile.AddressCity".
    /// </summary>
    /// <param name="path">Raw property path; segments may contain '-', '_' or mixed casing.</param>
    /// <returns>Normalized PascalCase property path. Returns empty string if <paramref name="path" /> is null or whitespace.</returns>
    public static string ToPascalCasePath(this string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
            segments[i] = ToPascalCaseInternal(segments[i]);
        return string.Join('.', segments);
    }

    #endregion
}