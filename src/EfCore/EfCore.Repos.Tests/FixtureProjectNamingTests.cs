namespace EfCore.Repos.Tests;

/// <summary>
///     <c>DKNet.EfCore.DtoEntities</c> was renamed to <c>EfCore.DtoGenerator.TestEntities</c> (folder, csproj,
///     namespaces) specifically so the fixture project no longer reads as a published library — a <c>DKNet.*</c>
///     name next to real packages invited exactly that confusion. Asserts the rename stuck and the project stays
///     unpacked.
/// </summary>
public class FixtureProjectNamingTests
{
    #region Fields

    private static readonly string ProjectDirectory = Path.Combine(
        TestPaths.SrcDirectory, "EfCore", "EfCore.DtoGenerator.TestEntities");

    private static readonly string ProjectFile = Path.Combine(
        ProjectDirectory, "EfCore.DtoGenerator.TestEntities.csproj");

    #endregion

    #region Methods

    [Fact]
    public void DtoGeneratorFixtureProject_FolderAndCsproj_AreNotDKNetNamed()
    {
        Directory.Exists(ProjectDirectory).ShouldBeTrue($"Expected fixture project at '{ProjectDirectory}'.");
        File.Exists(ProjectFile).ShouldBeTrue($"Expected csproj at '{ProjectFile}'.");

        Path.GetFileName(ProjectDirectory).ShouldNotStartWith("DKNet.");
        Path.GetFileNameWithoutExtension(ProjectFile).ShouldNotStartWith("DKNet.");
    }

    [Fact]
    public void DtoGeneratorFixtureProject_HasNoAssemblyNameOverride_ThatLooksLikeALibrary()
    {
        var csproj = File.ReadAllText(ProjectFile);
        var assemblyNameMatch = System.Text.RegularExpressions.Regex.Match(csproj, @"<AssemblyName>([^<]+)</AssemblyName>");

        // No override means the assembly name falls back to the (already non-DKNet-named) csproj file name.
        if (assemblyNameMatch.Success)
            assemblyNameMatch.Groups[1].Value.ShouldNotStartWith("DKNet.");
    }

    [Fact]
    public void DtoGeneratorFixtureProject_IsNotPackable()
    {
        var csproj = File.ReadAllText(ProjectFile);
        csproj.ShouldContain("<IsPackable>false</IsPackable>");
    }

    #endregion
}
