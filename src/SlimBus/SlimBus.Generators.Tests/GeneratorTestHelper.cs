using System;
using System.Collections.Immutable;
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

    // AppDomain.CurrentDomain.GetAssemblies() only returns assemblies already loaded into the process.
    // A ProjectReference alone does not force that — .NET loads assemblies lazily on first use — so touch
    // one public type from each reference the test sources need before collecting metadata references.
    // (DKNet.EfCore.Repos.Abstractions is retired/obsolete and unused by any test source here, so it isn't
    // force-loaded; its ProjectReference stays for parity with the brief's reference list.)
    private static readonly Type[] ForceLoadedAssemblies =
    [
        typeof(DKNet.EfCore.Abstractions.Entities.IEntity<object>),
        typeof(DKNet.EfCore.DtoGenerator.DtoGenerator),
        typeof(DKNet.SlimBus.Extensions.Fluents),
        // Fluents' generated requests implement SlimMessageBus.IRequest<T>; that assembly is only
        // pulled in by touching one of its own types, not by touching DKNet.SlimBus.Extensions alone.
        typeof(SlimMessageBus.IRequest<object>),
        typeof(FluentResults.Result),
        // Without this, [Required] fails to resolve while compiling the "Domain" source (its assembly
        // isn't loaded yet), and the attribute silently drops rather than raising a visible diagnostic.
        typeof(System.ComponentModel.DataAnnotations.RequiredAttribute)
    ];

    public static (Compilation Output, ImmutableArray<Diagnostic> Diagnostics, GeneratorDriverRunResult Result)
        Run(string domainSource, string apiSource)
    {
        _ = ForceLoadedAssemblies;

        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>().ToList();

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
}
