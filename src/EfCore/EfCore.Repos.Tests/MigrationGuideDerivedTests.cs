using System.Reflection;
using System.Text.RegularExpressions;
using DKNet.EfCore.Repos;
using DKNet.EfCore.Repos.Abstractions;
using DKNet.EfCore.Specifications;
using Microsoft.Extensions.DependencyInjection;

namespace EfCore.Repos.Tests;

/// <summary>
///     Reads <c>docs/EfCore/Migrating-Repos-To-Specifications.md</c> at test time and proves the migration steps it
///     advertises still name real members on both sides of the migration — the retired ("Before") API and its
///     <c>DKNet.EfCore.Specifications</c> ("After") replacement. The method names asserted on come from parsing the
///     document itself, not from a second hand-typed copy of the mapping table, so the test breaks the moment the
///     guide drifts from either API instead of silently going stale.
/// </summary>
public class MigrationGuideDerivedTests
{
    #region Fields

    private static readonly string DocPath = Path.Combine(
        TestPaths.RepoRootDirectory, "docs", "EfCore", "Migrating-Repos-To-Specifications.md");

    #endregion

    #region Methods

    /// <summary>Every non-generated public/non-public method name declared directly on a type in <paramref name="assembly" />.</summary>
    private static HashSet<string> DeclaredMemberNames(Assembly assembly) =>
        assembly.GetTypes()
            .Where(t => !t.Name.Contains('<', StringComparison.Ordinal)) // skip compiler-generated state machines
            .SelectMany(t => t.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance |
                BindingFlags.DeclaredOnly))
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Extracts identifier tokens (potential member names) from the backtick-quoted code spans of a markdown table cell.</summary>
    private static IEnumerable<string> IdentifiersInCodeSpans(string tableCell) =>
        Regex.Matches(tableCell, "`([^`]+)`")
            .SelectMany(m => Regex.Matches(m.Groups[1].Value, @"[A-Za-z_][A-Za-z0-9_]*"))
            .Select(m => m.Value);

    /// <summary>Parses the data rows of the "Query call-site mapping" table into (before-cell, after-cell) pairs.</summary>
    private static List<(string Before, string After)> ReadQueryCallSiteMappingRows()
    {
        var doc = File.ReadAllText(DocPath);
        var section = Regex.Match(
            doc, @"## Query call-site mapping\r?\n(.*?)(?:\r?\n## |\z)", RegexOptions.Singleline);
        section.Success.ShouldBeTrue("Doc must still contain a '## Query call-site mapping' section.");

        var rows = section.Groups[1].Value
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith('|') && !l.Contains("---", StringComparison.Ordinal))
            .Skip(1) // header row
            .Select(l => l.Trim('|').Split('|'))
            .Where(cells => cells.Length >= 2)
            .Select(cells => (Before: cells[0].Trim(), After: cells[1].Trim()))
            .ToList();

        rows.ShouldNotBeEmpty("Doc's mapping table must have at least one data row.");
        return rows;
    }

    [Fact]
    public void QueryCallSiteMappingTable_EveryBeforeCell_NamesARealRetiredMember()
    {
        var retiredNames = DeclaredMemberNames(typeof(IReadRepository<>).Assembly)
            .Union(DeclaredMemberNames(typeof(RepoExtensions).Assembly))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (before, _) in ReadQueryCallSiteMappingRows())
        {
            var candidates = IdentifiersInCodeSpans(before).ToList();
            candidates.ShouldNotBeEmpty($"Before-cell '{before}' should contain at least one code span.");
            candidates.ShouldContain(
                name => retiredNames.Contains(name),
                $"Before-cell '{before}' names no member found on the retired repository API " +
                "(DKNet.EfCore.Repos / DKNet.EfCore.Repos.Abstractions) — the guide has drifted.");
        }
    }

    [Fact]
    public void QueryCallSiteMappingTable_EveryAfterCell_NamesARealSpecificationsMember()
    {
        var specificationsNames = DeclaredMemberNames(typeof(IRepositorySpec).Assembly);

        foreach (var (_, after) in ReadQueryCallSiteMappingRows())
        {
            var candidates = IdentifiersInCodeSpans(after).ToList();
            candidates.ShouldNotBeEmpty($"After-cell '{after}' should contain at least one code span.");
            candidates.ShouldContain(
                name => specificationsNames.Contains(name),
                $"After-cell '{after}' names no member found on DKNet.EfCore.Specifications — the guide " +
                "advertises a replacement API that no longer exists (or was renamed).");
        }
    }

    [Fact]
    public void RegistrationSection_BeforeSnippet_NamesRealRetiredSetupMembers()
    {
        var doc = File.ReadAllText(DocPath);
        var section = Regex.Match(doc, @"## Registration\r?\n(.*?)\r?\n## ", RegexOptions.Singleline);
        section.Success.ShouldBeTrue("Doc must still contain a '## Registration' section.");

        var beforeFence = Regex.Match(section.Groups[1].Value, @"\*\*Before\*\*\s*```csharp(.*?)```", RegexOptions.Singleline);
        beforeFence.Success.ShouldBeTrue("Registration section must still have a **Before** code fence.");

        var calledMethods = Regex.Matches(beforeFence.Groups[1].Value, @"services\.(\w+)")
            .Select(m => m.Groups[1].Value)
            .ToList();
        calledMethods.ShouldNotBeEmpty();

        var setupMemberNames = DeclaredMemberNames(typeof(SetupRepository).Assembly);
        foreach (var method in calledMethods)
            setupMemberNames.ShouldContain(
                method, $"Registration guide's Before snippet calls services.{method}(), which no longer exists.");
    }

    [Fact]
    public void RegistrationSection_AfterSnippet_NamesRealSpecificationsSetupMember()
    {
        var doc = File.ReadAllText(DocPath);
        var section = Regex.Match(doc, @"## Registration\r?\n(.*?)\r?\n## ", RegexOptions.Singleline);
        section.Success.ShouldBeTrue("Doc must still contain a '## Registration' section.");

        var afterFence = Regex.Match(section.Groups[1].Value, @"\*\*After\*\*\s*```csharp(.*?)```", RegexOptions.Singleline);
        afterFence.Success.ShouldBeTrue("Registration section must still have an **After** code fence.");

        var calledMethods = Regex.Matches(afterFence.Groups[1].Value, @"services\.(\w+)")
            .Select(m => m.Groups[1].Value)
            .ToList();
        calledMethods.ShouldNotBeEmpty();

        var specSetupNames = DeclaredMemberNames(typeof(IRepositorySpec).Assembly);
        foreach (var method in calledMethods)
            specSetupNames.ShouldContain(
                method, $"Registration guide's After snippet calls services.{method}(), which does not exist on " +
                "DKNet.EfCore.Specifications.");
    }

    #endregion
}
