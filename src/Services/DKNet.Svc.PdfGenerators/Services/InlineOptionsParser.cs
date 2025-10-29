using System.Text;
using DKNet.Svc.PdfGenerators.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DKNet.Svc.PdfGenerators.Services;

/// <summary>
///     Parser for YAML front matter blocks within markdown files.
/// </summary>
public static class InlineOptionsParser
{
    #region Methods

    private static async Task<(bool success, string? content)> InternalTryReadYamlFrontMatter(string markdownFilePath)
    {
        string? markdownContent;
        using var reader = File.OpenText(markdownFilePath);
        var identifiers = new Dictionary<string, string>
        {
            { "---", "---" },
            { "<!--", "-->" }
        };

        var firstLine = await reader.ReadLineAsync();
        if (!identifiers.TryGetValue(firstLine!, out var endIdentifier))
        {
            markdownContent = null!;
            return (false, markdownContent);
        }

        var sb = new StringBuilder();

        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.Equals(line, endIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                markdownContent = sb.ToString();
                return (true, markdownContent);
            }

            sb.AppendLine(line);
        }

        // No end found
        markdownContent = null!;
        return (false, markdownContent);
    }

    /// <summary>
    ///     Parses the YAML front matter block at the beginning of the given markdown file.
    /// </summary>
    /// <param name="markdownFilePath">The path to the markdown file.</param>
    /// <returns>The parsed <see cref="PdfGeneratorOptions" /> from the markdown file.</returns>
    /// <exception cref="Exception"></exception>
    public static async Task<PdfGeneratorOptions> ParseYamlFrontMatter(string markdownFilePath)
    {
        var rs = await InternalTryReadYamlFrontMatter(markdownFilePath);
        if (!rs.success)
        {
            throw new InvalidDataException($"Could not find a YAML front matter block in '{markdownFilePath}'.");
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .Build();

        var options = deserializer.Deserialize<SerializableOptions>(rs.content!);
        return options?.ToPdfGeneratorOptions() ?? new PdfGeneratorOptions();
    }

    #endregion
}