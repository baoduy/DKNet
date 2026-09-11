// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;

namespace EfCore.DtoGenerator.Tests;

/// <summary>
///     In-process generator-driver tests pinning <c>DtoGenerator</c>'s attribute-argument emission
///     (DRK-1203 §3 rows 3-4, review follow-ups to PR #432): a null-valued constructor argument must not
///     emit a bare <c>null</c> literal into a <c>#nullable enable</c> generated file, and the spread form
///     used for <c>params</c> array arguments must never apply to a plain (non-<c>params</c>) array
///     parameter. Both probe attributes live in <c>System.ComponentModel.DataAnnotations</c> so
///     <see cref="DKNet.EfCore.DtoGenerator.DtoGenerator"/> carries them onto the generated DTO property
///     the same way it does any validation attribute.
/// </summary>
public class AttributeArgumentEmissionTests
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

    private const string ProbeAttributesSource = """
        using System;

        namespace System.ComponentModel.DataAnnotations
        {
            public sealed class ProbeNullArgAttribute : Attribute
            {
                public ProbeNullArgAttribute(string tag) => Tag = tag;
                public string Tag { get; }
            }

            public sealed class ProbeTagsAttribute : Attribute
            {
                public ProbeTagsAttribute(string[] tags) => Tags = tags;
                public string[] Tags { get; }
            }
        }
        """;

    private const string NullArgEntitySource = """
        namespace Probe.Entities
        {
            public sealed class NullArgProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeNullArg(null)]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string NonParamsArrayEntitySource = """
        namespace Probe.Entities
        {
            public sealed class TaggedProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeTags(new[] { "a", "b" })]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string EmptyNonParamsArrayEntitySource = """
        namespace Probe.Entities
        {
            public sealed class EmptyTagsProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeTags(new string[0])]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string NullElementNonParamsArrayEntitySource = """
        namespace Probe.Entities
        {
            public sealed class NullElementTagsProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeTags(new string[] { null })]
                public decimal Price { get; set; }
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
    public void NullValuedArgument_GeneratedSource_CompilesWithNoWarningOrAboveDiagnostic()
    {
        // Arrange
        var dtoSource = BuildDtoSource("NullArgProduct", "NullArgProductDto");

        // Act
        var diagnostics = Compile(NullArgEntitySource, dtoSource);

        // Assert
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
        diagnostics.ShouldNotContain(d => d.Id == "CS8625");
    }

    [Fact]
    public void NonParamsArrayArgument_IsEmittedAsArrayCreation_NotSpread()
    {
        // Arrange
        var dtoSource = BuildDtoSource("TaggedProduct", "TaggedProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(NonParamsArrayEntitySource, dtoSource);

        // Assert
        source.ShouldContain("new string[] {");
        source.ShouldNotContain("ProbeTags(\"a\", \"b\")");
        diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ShouldBeEmpty(
            string.Join('\n', diagnostics.Select(d => d.ToString())));
    }

    [Fact]
    public void EmptyNonParamsArrayArgument_GeneratedSource_CompilesWithNoWarningOrAboveDiagnostic()
    {
        // Arrange
        var dtoSource = BuildDtoSource("EmptyTagsProduct", "EmptyTagsProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(EmptyNonParamsArrayEntitySource, dtoSource);

        // Assert
        source.ShouldContain("new string[] {");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    [Fact]
    public void NullElementInNonParamsArrayArgument_GeneratedSource_CompilesWithNoWarningOrAboveDiagnostic()
    {
        // Arrange
        var dtoSource = BuildDtoSource("NullElementTagsProduct", "NullElementTagsProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(NullElementNonParamsArrayEntitySource, dtoSource);

        // Assert
        source.ShouldContain("new string[] {");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    #endregion

    #region Internals

    private static string BuildDtoSource(string entityName, string dtoName) =>
        $$"""
        using DKNet.EfCore.DtoGenerator;

        namespace Probe.Dtos
        {
            [GenerateDto(typeof(Probe.Entities.{{entityName}}))]
            public partial record {{dtoName}};
        }
        """;

    private static List<Diagnostic> Compile(string entitySource, string dtoSource) =>
        CompileAndCaptureSource(entitySource, dtoSource).Diagnostics;

    private static (string Source, List<Diagnostic> Diagnostics) CompileAndCaptureSource(
        string entitySource, string dtoSource)
    {
        var compilation = CSharpCompilation.Create(
            "ProbeCompilation",
            [
                CSharpSyntaxTree.ParseText(GenerateDtoAttributeSource),
                CSharpSyntaxTree.ParseText(ProbeAttributesSource),
                CSharpSyntaxTree.ParseText(entitySource),
                CSharpSyntaxTree.ParseText(dtoSource),
            ],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var generator = new DKNet.EfCore.DtoGenerator.DtoGenerator().AsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator], optionsProvider: new TestAnalyzerConfigOptionsProvider());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation, out _);
        var runResult = ((CSharpGeneratorDriver)driver).GetRunResult();

        var generatedSource = string.Join('\n', runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString()));
        generatedSource.ShouldNotBeNullOrEmpty("generator should have produced DTO source");

        // Diagnostics are scoped to the generator's own output trees, not the whole compilation — a
        // hand-authored [ProbeNullArg(null)] on the entity is the caller's own pre-existing warning
        // (unrelated to this generator) and must not be conflated with what the generator emits.
        var generatedTrees = runResult.GeneratedTrees.ToHashSet();
        var generatedSourceDiagnostics = updatedCompilation.GetDiagnostics()
            .Where(d => d.Location.SourceTree is not null && generatedTrees.Contains(d.Location.SourceTree))
            .ToList();

        return (generatedSource, generatedSourceDiagnostics);
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
