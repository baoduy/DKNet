using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Fw.Extensions.Tests.RepoGuards;

/// <summary>
///     Repository guards for the xunit 2 to xunit.v3 move (DRK-2622 §5): every test project, the snippet lab and
///     the <c>dknet-testing</c> skill must stay on xunit.v3. The guards read files only; they never restore,
///     build or shell out.
/// </summary>
public class XunitV3GuardTests
{
    #region Fields

    private const string LegacyXunit = "xunit";
    private const string XunitV3Prefix = "xunit.v3";

    #endregion

    #region Methods

    /// <summary>Scenario: Test code uses xunit.v3 only — each of the 27 test projects.</summary>
    [Fact]
    public void TestCodeUsesXunitV3Only_EachOfThe27TestProjects()
    {
        var src = Path.Combine(FindRepoRoot(), "src");
        var testProjects = Directory.EnumerateFiles(src, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !IsBuildOutput(p))
            .Where(p => Includes(p, "PackageReference").Contains("Microsoft.NET.Test.Sdk"))
            .ToList();

        testProjects.Count.ShouldBe(27, "test projects referencing Microsoft.NET.Test.Sdk under src/");
        Offenders(testProjects, "PackageReference").ShouldBeEmpty();
        Offenders([Path.Combine(src, "Directory.Packages.props")], "PackageVersion").ShouldBeEmpty();
    }

    /// <summary>Scenario: Test code uses xunit.v3 only — the snippet lab.</summary>
    [Fact]
    public void TestCodeUsesXunitV3Only_TheSnippetLab()
    {
        var lab = Path.Combine(FindRepoRoot(), "plugins", "dknet-skills", "tools", "snippet-lab");

        Offenders([Path.Combine(lab, "Directory.Packages.props")], "PackageVersion").ShouldBeEmpty();
        Offenders([Path.Combine(lab, "SnippetLab.csproj")], "PackageReference").ShouldBeEmpty();
    }

    /// <summary>Scenario: The testing skill tells readers to install xunit.v3.</summary>
    [Fact]
    public void TheTestingSkillTellsReadersToInstallXunitV3()
    {
        var skill = File.ReadAllText(Path.Combine(SkillDir(), "SKILL.md"));

        Regex.Matches(skill, @"^\|.*`dotnet add package xunit\.v3`", RegexOptions.Multiline).Count
            .ShouldBe(1, "install-table rows naming `dotnet add package xunit.v3` in dknet-testing/SKILL.md");
        Regex.Matches(skill, @"dotnet add package xunit(?!\.v3)").Select(m => m.Value).ShouldBeEmpty(
            "dknet-testing/SKILL.md must not tell readers to install xunit 2");
    }

    /// <summary>Scenario: The testing skill examples use the xunit.v3 setup shape.</summary>
    [Fact]
    public void TheTestingSkillExamplesUseTheXunitV3SetupShape()
    {
        var dir = SkillDir();
        var files = new[] { Path.Combine(dir, "SKILL.md") }
            .Concat(Directory.EnumerateFiles(Path.Combine(dir, "references"), "*.md"))
            .ToDictionary(f => Path.GetRelativePath(dir, f), File.ReadAllText);

        files.Values.Sum(text => Regex.Matches(text, "^```csharp", RegexOptions.Multiline).Count)
            .ShouldBe(17, "```csharp examples across dknet-testing/SKILL.md and references/*.md");

        var xunit2Lifetime = new Regex(@"(?<!Value)Task\s+(InitializeAsync|DisposeAsync)\s*\(");
        files.SelectMany(f => f.Value.Split('\n')
                .Select((line, i) => (line, i))
                .Where(x => xunit2Lifetime.IsMatch(x.line))
                .Select(x => $"{f.Key}:{x.i + 1}: {x.line.Trim()}"))
            .ToList()
            .ShouldBeEmpty("xunit 2 lifetime shape `Task InitializeAsync()`/`Task DisposeAsync()`; xunit.v3 uses ValueTask");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "DKNet.FW.sln"))) dir = dir.Parent;

        dir.ShouldNotBeNull("Could not locate the repo root (the directory holding src/DKNet.FW.sln) above the test output directory.");
        return dir.FullName;
    }

    private static string SkillDir() =>
        Path.Combine(FindRepoRoot(), "plugins", "dknet-skills", "skills", "dknet-testing");

    private static bool IsBuildOutput(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains("bin") || segments.Contains("obj");
    }

    private static List<string> Includes(string path, string element) =>
        XDocument.Load(path).Descendants(element)
            .Select(e => (string?)e.Attribute("Include"))
            .OfType<string>()
            .ToList();

    /// <summary>
    ///     Each file must carry no exact <c>xunit</c> entry and exactly one entry whose <c>Include</c> starts with
    ///     <c>xunit.v3</c>; every file that breaks either rule is listed with what it holds.
    /// </summary>
    private static List<string> Offenders(IEnumerable<string> paths, string element) =>
        paths.Select(p => (path: p, includes: Includes(p, element)))
            .Where(x => x.includes.Contains(LegacyXunit, StringComparer.OrdinalIgnoreCase)
                        || x.includes.Count(i => i.StartsWith(XunitV3Prefix, StringComparison.OrdinalIgnoreCase)) != 1)
            .Select(x => $"{x.path}: {element} xunit* = [{string.Join(", ", x.includes.Where(i => i.StartsWith(LegacyXunit, StringComparison.OrdinalIgnoreCase)))}]")
            .ToList();

    #endregion
}
