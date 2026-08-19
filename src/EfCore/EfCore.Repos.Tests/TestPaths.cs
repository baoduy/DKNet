namespace EfCore.Repos.Tests;

/// <summary>
///     Locates the checkout's <c>src/</c> and repository-root directories from the test binary's run location, so
///     tests can read source-tree files (docs, other projects' <c>.csproj</c>) without a hard-coded absolute path.
/// </summary>
internal static class TestPaths
{
    #region Properties

    /// <summary>The directory containing <c>DKNet.FW.sln</c>.</summary>
    public static string SrcDirectory { get; } = FindAncestorContaining(AppContext.BaseDirectory, "DKNet.FW.sln");

    /// <summary>The repository root (parent of <see cref="SrcDirectory" />).</summary>
    public static string RepoRootDirectory { get; } = Directory.GetParent(SrcDirectory)!.FullName;

    #endregion

    #region Methods

    private static string FindAncestorContaining(string startDirectory, string marker)
    {
        DirectoryInfo? dir = new(startDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, marker))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate an ancestor of '{startDirectory}' containing '{marker}'.");
    }

    #endregion
}
