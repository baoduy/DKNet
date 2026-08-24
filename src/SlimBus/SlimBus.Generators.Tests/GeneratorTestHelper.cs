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

    public static (Compilation Output, ImmutableArray<Diagnostic> Diagnostics, GeneratorDriverRunResult Result)
        Run(string domainSource, string apiSource)
    {
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

        var driver = CSharpGeneratorDriver.Create(new CrudGenerator());
        driver.RunGeneratorsAndUpdateCompilation(api, out var output, out var diagnostics);
        return (output, diagnostics, ((GeneratorDriver)driver).GetRunResult());
    }
}
