using System.Collections.Immutable;
using DKNet.EfCore.DtoGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;

namespace EfCore.DtoGenerator.Tests;

/// <summary>
///     In-process generator-driver tests proving a <c>[SensitiveData]</c> declaration on an entity
///     property is carried onto the generated response model (DRK-1183 §5), driving
///     <see cref="DtoGenerator"/> directly against an in-memory compilation — same harness as
///     <see cref="DtoGeneratorPrecedenceTests"/>.
/// </summary>
public class SensitiveDataDtoGenerationTests
{
    #region Constants

    private const string GenerateDtoAttributeSource = """
        using System;

        namespace DKNet.EfCore.DtoGenerator
        {
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
            internal sealed class GenerateDtoAttribute : Attribute
            {
                public GenerateDtoAttribute(Type entityType) => EntityType = entityType;
                public Type EntityType { get; }
                public string[] Exclude { get; set; } = [];
                public string[] Include { get; set; } = [];
                public bool IgnoreComplexType { get; set; }
            }
        }
        """;

    // Mirrors the real DKNet.EfCore.Abstractions.Attributes.SensitiveDataAttribute shape — the
    // generator matches it by namespace + name string only and must never reference the real type
    // (DRK-1183 §4 invariant), so the probe compilation supplies its own stub.
    private const string SensitiveDataAttributeSource = """
        using System;
        using System.Collections.Generic;

        namespace DKNet.EfCore.Abstractions.Attributes
        {
            [AttributeUsage(AttributeTargets.Property, Inherited = false)]
            public sealed class SensitiveDataAttribute : Attribute
            {
                public SensitiveDataAttribute(params string[] roles) => Roles = roles ?? Array.Empty<string>();
                public SensitiveDataAttribute() : this(Array.Empty<string>())
                {
                }
                public IReadOnlyList<string> Roles { get; }
            }
        }
        """;

    private const string EntitySource = """
        namespace Probe.Entities
        {
            public sealed class Product
            {
                public int ProductId { get; set; }
                public string Name { get; set; } = string.Empty;

                [DKNet.EfCore.Abstractions.Attributes.SensitiveData("pricing")]
                public decimal SupplierCostPrice { get; set; }

                [DKNet.EfCore.Abstractions.Attributes.SensitiveData]
                public string SupplierReferenceCode { get; set; } = string.Empty;
            }
        }
        """;

    // Regression probe for a review finding (DRK-1185 round 1): an explicit `null` array constructor
    // argument — a pre-existing System.ComponentModel.DataAnnotations shape too, [SensitiveData] just
    // takes the identical path — must not abort generation.
    private const string EntityWithNullRolesSource = """
        namespace Probe.Entities
        {
            public sealed class ProductWithNullRoles
            {
                public int ProductId { get; set; }

                [DKNet.EfCore.Abstractions.Attributes.SensitiveData(null)]
                public decimal SupplierCostPrice { get; set; }
            }
        }
        """;

    private static readonly MetadataReference[] References =
    [
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(ImmutableArray<>).Assembly.Location),
    ];

    #endregion

    #region Methods

    [Fact]
    public void RoleNamedDeclaration_ReachesGeneratedModel_AsSensitiveDataWithRoleArgument()
    {
        // Act
        var source = RunGenerator();

        // Assert
        source.ShouldContain("[SensitiveData(\"pricing\")]");
    }

    [Fact]
    public void RoleNamedDeclaration_ReachesGeneratedModel_WithAttributesNamespaceImported()
    {
        // Act
        var source = RunGenerator();

        // Assert
        source.ShouldContain("using DKNet.EfCore.Abstractions.Attributes;");
    }

    [Fact]
    public void RoleLessDeclaration_ReachesGeneratedModel_WithoutEmptyArrayLiteral()
    {
        // Act
        var source = RunGenerator();

        // Assert
        source.ShouldContain("[SensitiveData]");
        source.ShouldNotContain("new[] {  }");
        source.ShouldNotContain("SensitiveData(new[]");
    }

    [Fact]
    public void RoleLessDeclaration_GeneratedSource_CompilesCleanly()
    {
        // Arrange
        var dtoSource = BuildDtoSource();
        var compilation = CSharpCompilation.Create(
            "ProbeCompilation",
            [
                CSharpSyntaxTree.ParseText(GenerateDtoAttributeSource),
                CSharpSyntaxTree.ParseText(SensitiveDataAttributeSource),
                CSharpSyntaxTree.ParseText(EntitySource),
                CSharpSyntaxTree.ParseText(dtoSource),
            ],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DKNet.EfCore.DtoGenerator.DtoGenerator().AsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator], optionsProvider: new TestAnalyzerConfigOptionsProvider());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation, out _);

        // Act
        var diagnostics = updatedCompilation.GetDiagnostics();

        // Assert
        diagnostics.ShouldNotContain(d => d.Severity == DiagnosticSeverity.Error,
            string.Join('\n', diagnostics.Select(d => d.ToString())));
    }

    [Fact]
    public void NullRolesArgument_StillGeneratesSource_WithNoDiagnostic()
    {
        // Arrange
        var compilation = CSharpCompilation.Create(
            "NullRolesProbeCompilation",
            [
                CSharpSyntaxTree.ParseText(GenerateDtoAttributeSource),
                CSharpSyntaxTree.ParseText(SensitiveDataAttributeSource),
                CSharpSyntaxTree.ParseText(EntityWithNullRolesSource),
                CSharpSyntaxTree.ParseText(BuildNullRolesDtoSource()),
            ],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DKNet.EfCore.DtoGenerator.DtoGenerator().AsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator], optionsProvider: new TestAnalyzerConfigOptionsProvider());

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var runResult = ((CSharpGeneratorDriver)driver).GetRunResult();

        // Assert
        runResult.Diagnostics.ShouldNotContain(d => d.Id == "DKDTOGEN001",
            string.Join('\n', runResult.Diagnostics.Select(d => d.ToString())));
        runResult.Results.SelectMany(r => r.GeneratedSources).ShouldNotBeEmpty(
            "generator should still produce DTO source for a null-roles [SensitiveData] declaration");
    }

    #endregion

    #region Internals

    private static string BuildDtoSource() =>
        """
        using DKNet.EfCore.DtoGenerator;

        namespace Probe.Dtos
        {
            [GenerateDto(typeof(Probe.Entities.Product))]
            public partial record ProductDto;
        }
        """;

    private static string BuildNullRolesDtoSource() =>
        """
        using DKNet.EfCore.DtoGenerator;

        namespace Probe.Dtos
        {
            [GenerateDto(typeof(Probe.Entities.ProductWithNullRoles))]
            public partial record ProductWithNullRolesDto;
        }
        """;

    private static string RunGenerator()
    {
        var dtoSource = BuildDtoSource();
        var compilation = CSharpCompilation.Create(
            "ProbeCompilation",
            [
                CSharpSyntaxTree.ParseText(GenerateDtoAttributeSource),
                CSharpSyntaxTree.ParseText(SensitiveDataAttributeSource),
                CSharpSyntaxTree.ParseText(EntitySource),
                CSharpSyntaxTree.ParseText(dtoSource),
            ],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DKNet.EfCore.DtoGenerator.DtoGenerator().AsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator], optionsProvider: new TestAnalyzerConfigOptionsProvider());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var runResult = ((CSharpGeneratorDriver)driver).GetRunResult();
        var generatedSources = runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString())
            .ToList();

        generatedSources.ShouldNotBeEmpty("generator should have produced DTO source");
        return generatedSources.Count == 1 ? generatedSources[0] : string.Join('\n', generatedSources);
    }

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly TestAnalyzerConfigOptions _globalOptions = new();

        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _globalOptions;
    }

    private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            value = string.Empty;
            return false;
        }
    }

    #endregion
}
