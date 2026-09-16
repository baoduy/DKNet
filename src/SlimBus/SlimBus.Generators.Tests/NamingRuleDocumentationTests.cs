using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace SlimBus.Generators.Tests;

/// <summary>
///     DRK-1327 §5 scenario 8 (@unit): "The documented naming rule matches the names the generator produces."
///     Reads the worked example from <c>docs/Messaging/DKNet.SlimBus.Generators.md</c> (read-only fixture —
///     never edited here, docs-writer owns it per DRK-1336 §3 row 9) and cross-checks it against a real
///     generator run: the entity/DTO source in the ```csharp crud-naming-example``` fenced block, generated,
///     must emit a <c>ValidateRouteNames(...)</c> call whose names are set-equal to the
///     ```text crud-naming-routes``` fenced block, one name per line.
/// </summary>
public class NamingRuleDocumentationTests
{
    private const string DocRelativePath = "docs/Messaging/DKNet.SlimBus.Generators.md";

    [Fact]
    public void DocumentedNamingRule_MatchesTheNamesTheGeneratorProduces()
    {
        // DKNet.FW.sln lives in src/, one level below the repo root that docs/ hangs off.
        var repoRoot = Directory.GetParent(FindSrcDirectory())!.FullName;
        var doc = File.ReadAllText(Path.Combine(repoRoot, DocRelativePath));

        var exampleSource = ExtractFencedBlock(doc, "csharp", "crud-naming-example");
        var documentedNames = ExtractFencedBlock(doc, "text", "crud-naming-routes")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // The example is self-contained (entity + its [GenerateDto] DTO in one block, per row 9), so it is
        // compiled as the "MyApi" side alone; CrudModelBuilder finds the entity by symbol, not by which
        // compilation declared it — the same cross-assembly path GeneratorTestHelper's own split exercises.
        var (_, _, result) = GeneratorTestHelper.Run("namespace Empty { }", exampleSource);
        var generatedText = GeneratorTestHelper.GeneratedText(result);

        var call = Regex.Match(generatedText, """ValidateRouteNames\(\s*"[^"]+"\s*(?:,\s*"(?<name>[^"]+)"\s*)+\);""");
        call.Success.ShouldBeTrue($"Expected a ValidateRouteNames(...) call in the generated output, found none:\n{generatedText}");

        var emittedNames = call.Groups["name"].Captures.Select(c => c.Value).ToArray();
        emittedNames.ShouldBe(documentedNames, ignoreOrder: true);
    }

    private static string ExtractFencedBlock(string doc, string language, string tag)
    {
        var match = Regex.Match(doc, $"""```{Regex.Escape(language)} {Regex.Escape(tag)}\r?\n(.*?)```""", RegexOptions.Singleline);
        return match.Success
            ? match.Groups[1].Value
            : throw new InvalidOperationException(
                $"{DocRelativePath} has no ```{language} {tag} ... ``` fenced block yet (DRK-1336 §3 row 9).");
    }

    private static string FindSrcDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DKNet.FW.sln"))) dir = dir.Parent;

        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate DKNet.FW.sln above the test output directory.");
    }
}
