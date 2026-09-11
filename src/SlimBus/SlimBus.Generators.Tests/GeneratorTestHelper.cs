using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using DKNet.SlimBus.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SlimBus.Generators.Tests;

/// <summary>
/// Drives <see cref="CrudGenerator"/> against two synthetic compilations: a "Domain" assembly (compiled
/// separately and referenced by metadata, exercising the cross-assembly discovery path) and a "MyApi"
/// assembly that references it.
/// </summary>
internal static class GeneratorTestHelper
{
    // The real DKNet.EfCore.DtoGenerator.GenerateDtoAttribute is `internal`; real NuGet consumers get its
    // source file compiled directly into their own assembly via a packed content file, so it stays
    // accessible there. These in-memory compilations aren't real NuGet consumers, so declare a
    // compilation-local stand-in with the same full metadata name (DKNet.EfCore.DtoGenerator.GenerateDtoAttribute)
    // instead of fighting InternalsVisibleTo across an in-memory assembly boundary. The real (internal,
    // inaccessible) type from the referenced DtoGenerator assembly is shadowed with a CS0436 warning, not
    // an error.
    private const string GenerateDtoAttributeShim = """
        namespace DKNet.EfCore.DtoGenerator
        {
            public sealed class GenerateDtoAttribute : System.Attribute
            {
                public GenerateDtoAttribute(System.Type entityType) => EntityType = entityType;
                public System.Type EntityType { get; }
            }
        }
        """;

    // The reference set comes from the test host's trusted-platform-assemblies list rather than
    // AppDomain.CurrentDomain.GetAssemblies(): the latter only contains assemblies already loaded into the
    // process, which .NET loads lazily on first use, so the set (and which tests pass) depended on
    // process-wide load order — which other test classes had already run first. TPA lists every assembly the
    // test host resolved from its deps.json up front, so it doesn't have that problem.
    // (DKNet.EfCore.Repos.Abstractions is retired/obsolete and unused by any test source here, so neither it
    // nor a ProjectReference to it belongs in this project — see RetiredLibraryDependencyBoundaryTests.)
    private static readonly Lazy<ImmutableArray<(string Name, MetadataReference Reference)>> TrustedPlatformReferences = new(() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(File.Exists)
            .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(path => (Name: Path.GetFileNameWithoutExtension(path), Reference: (MetadataReference)MetadataReference.CreateFromFile(path)))
            .ToImmutableArray());

    /// <summary>
    /// Runs the generator as <see cref="Run(string, string)" />, except any reference whose simple assembly
    /// name appears in <paramref name="excludedAssemblyNames" /> is left out of the "MyApi" compilation's
    /// references — used to exercise generator behavior when a given assembly isn't referenced.
    /// </summary>
    public static (Compilation Output, ImmutableArray<Diagnostic> Diagnostics, GeneratorDriverRunResult Result)
        Run(string domainSource, string apiSource, string[]? excludedAssemblyNames = null)
    {
        var refs = TrustedPlatformReferences.Value
            .Where(r => excludedAssemblyNames is null || !excludedAssemblyNames.Contains(r.Name))
            .Select(r => r.Reference)
            .ToList();

        var domain = CSharpCompilation.Create("Domain",
            [CSharpSyntaxTree.ParseText(domainSource)], refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var api = CSharpCompilation.Create("MyApi",
            [CSharpSyntaxTree.ParseText(apiSource), CSharpSyntaxTree.ParseText(GenerateDtoAttributeShim)],
            [.. refs, domain.ToMetadataReference()],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // GeneratorDriver is immutable: RunGeneratorsAndUpdateCompilation returns the driver that actually
        // ran (carrying the run's tracked results) rather than mutating the original instance in place.
        var driver = CSharpGeneratorDriver.Create(new CrudGenerator());
        var ranDriver = driver.RunGeneratorsAndUpdateCompilation(api, out var output, out var diagnostics);
        return (output, diagnostics, ranDriver.GetRunResult());
    }

    /// <summary>
    /// Joins every generated source's text, with line endings normalised to <c>\n</c>.
    /// </summary>
    /// <param name="result">The generator run result to read generated sources from.</param>
    /// <returns>The concatenated generated source text, using only <c>\n</c> as a line separator.</returns>
    public static string GeneratedText(GeneratorDriverRunResult result) =>
        // CrudGenerator builds its output via StringBuilder.AppendLine, which writes Environment.NewLine —
        // \r\n on Windows. Normalising here keeps assertions that embed a literal \n host-independent.
        string.Join("\n", result.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
}
