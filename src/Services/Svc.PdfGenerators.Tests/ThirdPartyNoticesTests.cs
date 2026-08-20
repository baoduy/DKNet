using System.Xml.Linq;
using Shouldly;

namespace Svc.PdfGenerators.Tests;

/// <summary>
///     Verifies the DKNet.Svc.PdfGenerators package correctly attributes its upstream fork
///     source (Markdown2Pdf) per DRK-580: a THIRD-PARTY-NOTICES.md carrying the verbatim
///     upstream MIT notice, packed into the nupkg, plus a fork statement in the package
///     Description.
/// </summary>
public class ThirdPartyNoticesTests
{
    private const string UpstreamCopyrightLine = "Copyright (c) 2023 Flayms";

    #region Methods

    [Fact]
    public void ThirdPartyNoticesFile_Exists()
    {
        // Arrange & Act
        var path = GetNoticesFilePath();

        // Assert
        File.Exists(path).ShouldBeTrue($"THIRD-PARTY-NOTICES.md not found at {path}");
    }

    [Fact]
    public void ThirdPartyNoticesFile_ContainsVerbatimUpstreamCopyrightNotice()
    {
        // Arrange
        var content = File.ReadAllText(GetNoticesFilePath());

        // Act & Assert
        content.ShouldContain(UpstreamCopyrightLine);
        content.ShouldContain("Markdown2Pdf");
        content.ShouldContain("MIT License");
        content.ShouldContain("THE SOFTWARE IS PROVIDED \"AS IS\"");
    }

    [Fact]
    public void ProjectFile_PacksThirdPartyNoticesIntoNupkg()
    {
        // Arrange
        var csproj = XDocument.Load(GetProjectFilePath());

        // Act
        var isPacked = csproj.Descendants("None")
            .Where(e => (string?)e.Attribute("Include") == "THIRD-PARTY-NOTICES.md")
            .Any(e => string.Equals((string?)e.Attribute("Pack"), "true", StringComparison.OrdinalIgnoreCase));

        // Assert
        isPacked.ShouldBeTrue("THIRD-PARTY-NOTICES.md must be Pack=\"true\" so it ships inside the nupkg");
    }

    [Fact]
    public void ProjectFile_DescriptionStatesForkOfMarkdown2Pdf()
    {
        // Arrange
        var csproj = XDocument.Load(GetProjectFilePath());

        // Act
        var description = csproj.Descendants("Description").FirstOrDefault()?.Value;

        // Assert
        description.ShouldNotBeNullOrWhiteSpace();
        description.ShouldContain("fork of Markdown2Pdf");
    }

    /// <summary>Walks up from the test assembly's location to the repo's `src/` directory (marked by DKNet.FW.sln).</summary>
    private static string GetSrcDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DKNet.FW.sln")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate src/ (DKNet.FW.sln) above the test assembly.");
    }

    private static string GetProjectFilePath() =>
        Path.Combine(GetSrcDirectory(), "Services", "DKNet.Svc.PdfGenerators", "DKNet.Svc.PdfGenerators.csproj");

    private static string GetNoticesFilePath() =>
        Path.Combine(GetSrcDirectory(), "Services", "DKNet.Svc.PdfGenerators", "THIRD-PARTY-NOTICES.md");

    #endregion
}
