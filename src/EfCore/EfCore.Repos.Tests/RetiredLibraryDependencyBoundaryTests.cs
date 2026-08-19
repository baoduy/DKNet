using System.Text.RegularExpressions;

namespace EfCore.Repos.Tests;

/// <summary>
///     Enforces that nothing in the solution — other than this test project, the sole permitted consumer — still
///     depends on the retired <c>DKNet.EfCore.Repos</c> / <c>DKNet.EfCore.Repos.Abstractions</c> libraries. Reads
///     every <c>.csproj</c> under <c>src/</c> directly, so a new <c>ProjectReference</c> added anywhere else fails
///     this test instead of silently reviving a dependency the retirement was meant to sever.
/// </summary>
public class RetiredLibraryDependencyBoundaryTests
{
    #region Fields

    /// <summary>
    ///     For each retired csproj, the project (by name, no extension) allowed to hold a
    ///     <c>&lt;ProjectReference&gt;</c> to it: this test project (the sole permitted consumer) plus, for the
    ///     Abstractions library only, its own paired implementation project (Repos implementing Repos.Abstractions
    ///     is internal to the retired pair, not a new external consumer).
    /// </summary>
    private static readonly Dictionary<string, string[]> AllowedReferencers = new()
    {
        ["DKNet.EfCore.Repos.csproj"] = ["EfCore.Repos.Tests"],
        ["DKNet.EfCore.Repos.Abstractions.csproj"] = ["EfCore.Repos.Tests", "DKNet.EfCore.Repos"]
    };

    #endregion

    #region Methods

    private static IEnumerable<string> AllCsprojFiles() =>
        Directory.EnumerateFiles(TestPaths.SrcDirectory, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    [Theory]
    [InlineData("DKNet.EfCore.Repos.csproj")]
    [InlineData("DKNet.EfCore.Repos.Abstractions.csproj")]
    public void OnlyAllowedProjects_MayReferenceRetiredLibrary(string retiredProjectFile)
    {
        var allowed = AllowedReferencers[retiredProjectFile];

        var referencingProjects = AllCsprojFiles()
            .Where(f => Path.GetFileName(f) != retiredProjectFile) // the retired project doesn't "depend on" itself
            .Where(f => Regex.IsMatch(
                File.ReadAllText(f),
                $@"<ProjectReference\s+Include=""[^""]*{Regex.Escape(retiredProjectFile)}"""))
            .Select(f => Path.GetFileNameWithoutExtension(f)!)
            .ToList();

        var unexpected = referencingProjects.Except(allowed).ToList();
        unexpected.ShouldBeEmpty(
            $"Only {string.Join(" and ", allowed)} may depend on the retired {retiredProjectFile}. " +
            $"Found unexpected referencer(s): {string.Join(", ", unexpected)}");
    }

    #endregion
}
