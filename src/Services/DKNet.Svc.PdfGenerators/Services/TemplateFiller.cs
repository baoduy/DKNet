using System.Text.RegularExpressions;

namespace DKNet.Svc.PdfGenerators.Services;

/// <summary>
///     Simple templating service.
/// </summary>
/// <summary>
///     Provides TemplateFiller functionality.
/// </summary>
public static class TemplateFiller
{
    #region Fields

    /// <summary>
    ///     matches groups like @(myToken).
    /// </summary>
    private static readonly Regex TokenRegex = new(
        @"(?<token>@\(.*?\))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

    #endregion

    #region Methods

    /// <summary>
    ///     Replaces all tokens of the form <i>@(key)</i> with the given values in the <paramref name="model" />.
    /// </summary>
    /// <param name="template">The template to replace in.</param>
    /// <param name="model">The model, containg the keys and values.</param>
    /// <returns>The filled template.</returns>
    /// <summary>
    ///     FillTemplate operation.
    /// </summary>
    public static string FillTemplate(string template, Dictionary<string, string> model)
    {
        var matches = TokenRegex.Matches(template);

        var filled = template;

        foreach (Match match in matches)
        {
            var token = match.Groups["token"].Value;
            var keyName = token.Replace("@", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("(", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(")", string.Empty, StringComparison.OrdinalIgnoreCase);

            if (!model.TryGetValue(keyName, out var value))
            {
                value = string.Empty;
            }

            filled = filled.Replace(token, value, StringComparison.OrdinalIgnoreCase);
        }

        return filled;
    }

    #endregion
}