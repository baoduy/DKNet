using System.Text;
using DKNet.Svc.PdfGenerator.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DKNet.Svc.PdfGenerator.Services;

/// <summary>
/// Parser for YAML front matter blocks within markdown files.
/// </summary>
public class InlineOptionsParser
{
    /// <summary>
    /// Parses the YAML front matter block at the beginning of the given markdown file.
    /// </summary>
    /// <param name="markdownFilePath">The path to the markdown file.</param>
    /// <returns>The parsed <see cref="Markdown2PdfOptions"/> from the markdown file.</returns>
    /// <exception cref="Exception"></exception>
    public static Markdown2PdfOptions ParseYamlFrontMatter(string markdownFilePath)
    {
        if (!InternalTryReadYamlFrontMatter(markdownFilePath, out var yamlContent))
            throw new Exception($"Could not find a YAML front matter block in '{markdownFilePath}'.");

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .Build();

        var options = deserializer.Deserialize<SerializableOptions>(yamlContent);
        return options.ToMarkdown2PdfOptions();
    }

    private static bool InternalTryReadYamlFrontMatter(string markdownFilePath, out string markdownContent)
    {
        using var reader = File.OpenText(markdownFilePath);

        var identifiers = new Dictionary<string, string>
        {
            { "---", "---" },
            { "<!--", "-->" }
        };

        var firstLine = reader.ReadLine();
        if (!identifiers.TryGetValue(firstLine!, out var endIdentifier))
        {
            markdownContent = null!;
            return false;
        }

        var sb = new StringBuilder();

        while (reader.ReadLine() is { } line)
        {
            if (string.Equals(line, endIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                markdownContent = sb.ToString();
                return true;
            }

            sb.AppendLine(line);
        }

        // No end found
        markdownContent = null!;
        return false;
    }
}