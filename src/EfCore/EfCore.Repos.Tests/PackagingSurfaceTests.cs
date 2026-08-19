using System.Text.RegularExpressions;

namespace EfCore.Repos.Tests;

/// <summary>
///     Verifies the pack surface this cycle promised: <c>DKNet.AspCore.Extensions</c> is now packed and advertised;
///     <c>DKNet.EfCore.Repos</c>, <c>DKNet.EfCore.Repos.Abstractions</c>, and the renamed
///     <c>DKNet.EfCore.DtoEntities</c> fixture are not, and no top-level "install a package" doc still tells a
///     reader to <c>dotnet add package</c> one of the retired names.
/// </summary>
public class PackagingSurfaceTests
{
    #region Fields

    /// <summary>
    ///     The repo's package-directory-style docs — pages whose job is to enumerate installable packages, as
    ///     opposed to a retired package's own README (which legitimately keeps its own history/migration content).
    /// </summary>
    private static readonly string[] AdvertisedPackageListDocs =
    [
        Path.Combine(TestPaths.RepoRootDirectory, "README.md"),
        Path.Combine(TestPaths.RepoRootDirectory, "docs", "README.md"),
        Path.Combine(TestPaths.RepoRootDirectory, "docs", "EfCore", "README.md"),
        Path.Combine(TestPaths.SrcDirectory, "README.md")
    ];

    private static readonly string[] RetiredPackageIds =
    [
        "DKNet.EfCore.Repos.Abstractions",
        "DKNet.EfCore.Repos",
        "DKNet.EfCore.DtoEntities"
    ];

    #endregion

    #region Methods

    private static bool IsPackable(string projectDirectoryName, string csprojFileName)
    {
        var path = Path.Combine(TestPaths.SrcDirectory, "EfCore", projectDirectoryName, csprojFileName);
        File.Exists(path).ShouldBeTrue($"Expected csproj at '{path}'.");
        return Regex.IsMatch(File.ReadAllText(path), @"<IsPackable>\s*true\s*</IsPackable>", RegexOptions.IgnoreCase);
    }

    [Fact]
    public void AspCoreExtensions_IsPackable()
    {
        var path = Path.Combine(
            TestPaths.SrcDirectory, "AspNet", "DKNet.AspCore.Extensions", "DKNet.AspCore.Extensions.csproj");
        File.Exists(path).ShouldBeTrue($"Expected csproj at '{path}'.");
        Regex.IsMatch(File.ReadAllText(path), @"<IsPackable>\s*true\s*</IsPackable>", RegexOptions.IgnoreCase)
            .ShouldBeTrue("DKNet.AspCore.Extensions must be packable — it is the package this cycle publishes.");
    }

    [Theory]
    [InlineData("DKNet.EfCore.Repos", "DKNet.EfCore.Repos.csproj")]
    [InlineData("DKNet.EfCore.Repos.Abstractions", "DKNet.EfCore.Repos.Abstractions.csproj")]
    [InlineData("EfCore.DtoGenerator.TestEntities", "EfCore.DtoGenerator.TestEntities.csproj")]
    public void RetiredOrRenamedProject_IsNotPackable(string projectDirectoryName, string csprojFileName)
    {
        IsPackable(projectDirectoryName, csprojFileName).ShouldBeFalse(
            $"{csprojFileName} must not be packable — it is retired/unpublished this cycle.");
    }

    [Fact]
    public void AdvertisedPackageListDocs_DoNotTellReadersToInstallARetiredPackage()
    {
        foreach (var doc in AdvertisedPackageListDocs)
        {
            File.Exists(doc).ShouldBeTrue($"Expected package-list doc at '{doc}'.");
            var content = File.ReadAllText(doc);

            foreach (var retiredId in RetiredPackageIds)
                content.ShouldNotContain(
                    $"dotnet add package {retiredId}",
                    customMessage: $"'{doc}' still advertises 'dotnet add package {retiredId}', which is no longer packed.");
        }
    }

    #endregion
}
