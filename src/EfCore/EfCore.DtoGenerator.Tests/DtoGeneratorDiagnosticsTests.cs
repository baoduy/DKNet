// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;

namespace EfCore.DtoGenerator.Tests;

/// <summary>
///     In-process generator-driver tests for <see cref="DKNet.EfCore.DtoGenerator.DtoGenerator"/>'s
///     diagnostic paths that the compile-time <c>EfCore.DtoGenerator.TestEntities</c> fixtures don't
///     reach: a zero-property entity (<c>DKDTOGEN002</c>), a DTO specifying both <c>Include</c> and
///     <c>Exclude</c> (<c>DKDTOGEN004</c>), and the collection-expression form of <c>Include</c>/<c>Exclude</c>
///     (<c>["Name"]</c> rather than <c>new[] { "Name" }</c>).
/// </summary>
public class DtoGeneratorDiagnosticsTests
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

    private static readonly MetadataReference[] References =
    [
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(ImmutableArray<>).Assembly.Location),
    ];

    #endregion

    #region Methods

    [Fact]
    public void EntityWithNoPublicProperties_ReportsNoPropertiesFoundWarning()
    {
        // Arrange
        const string entitySource = """
            namespace Probe.Entities
            {
                public sealed class EmptyEntity
                {
                }
            }
            """;
        const string dtoSource = """
            using DKNet.EfCore.DtoGenerator;

            namespace Probe.Dtos
            {
                [GenerateDto(typeof(Probe.Entities.EmptyEntity))]
                public partial record EmptyEntityDto;
            }
            """;

        // Act
        var (diagnostics, _) = RunGenerator(entitySource, dtoSource);

        // Assert
        diagnostics.ShouldContain(d => d.Id == "DKDTOGEN002" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void IncludeAndExcludeBothSpecified_ReportsMutuallyExclusiveWarning_AndSkipsGeneration()
    {
        // Arrange
        const string entitySource = """
            namespace Probe.Entities
            {
                public sealed class ConflictEntity
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = string.Empty;
                }
            }
            """;
        const string dtoSource = """
            using DKNet.EfCore.DtoGenerator;

            namespace Probe.Dtos
            {
                [GenerateDto(typeof(Probe.Entities.ConflictEntity), Include = new[] { "Name" }, Exclude = new[] { "Id" })]
                public partial record ConflictEntityDto;
            }
            """;

        // Act
        var (diagnostics, generatedSources) = RunGenerator(entitySource, dtoSource);

        // Assert
        diagnostics.ShouldContain(d => d.Id == "DKDTOGEN004" && d.Severity == DiagnosticSeverity.Warning);
        generatedSources.ShouldBeEmpty("generation must be skipped when Include and Exclude both carry properties");
    }

    [Fact]
    public void CollectionExpressionInclude_NarrowsToNamedProperties()
    {
        // Arrange
        const string entitySource = """
            namespace Probe.Entities
            {
                public sealed class NarrowedEntity
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = string.Empty;
                    public string Secret { get; set; } = string.Empty;
                }
            }
            """;
        const string dtoSource = """
            using DKNet.EfCore.DtoGenerator;

            namespace Probe.Dtos
            {
                [GenerateDto(typeof(Probe.Entities.NarrowedEntity), Include = ["Name"])]
                public partial record NarrowedEntityDto;
            }
            """;

        // Act
        var (_, generatedSources) = RunGenerator(entitySource, dtoSource);
        var source = string.Join('\n', generatedSources.Select(s => s.SourceText.ToString()));

        // Assert
        source.ShouldContain("Name");
        source.ShouldNotContain("Secret");
        source.ShouldNotContain(" Id ");
    }

    [Fact]
    public void HandAuthoredProperty_IsNotDuplicatedByGenerator()
    {
        // Arrange
        const string entitySource = """
            namespace Probe.Entities
            {
                public sealed class PartialEntity
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = string.Empty;
                }
            }
            """;
        const string dtoSource = """
            using DKNet.EfCore.DtoGenerator;

            namespace Probe.Dtos
            {
                [GenerateDto(typeof(Probe.Entities.PartialEntity))]
                public partial record PartialEntityDto
                {
                    public string Name { get; set; } = "hand-authored";
                }
            }
            """;

        // Act
        var (_, generatedSources) = RunGenerator(entitySource, dtoSource);
        var source = string.Join('\n', generatedSources.Select(s => s.SourceText.ToString()));

        // Assert
        source.ShouldContain("Id");
        source.ShouldNotContain("init; }\n    public string Name");
        source.ShouldNotContain("Gets or sets the Name.");
    }

    /// <summary>
    ///     One entity carrying every scalar/array/generic-collection/nullable-value-type/complex-reference
    ///     shape <c>BuildCleanTypeName</c> and <c>IsComplexNavigationType</c> branch on, exercised with the
    ///     built-in default <c>IgnoreComplexType</c> (unset, defaults to <see langword="true"/>): a plain
    ///     navigation class is dropped, an <c>[Owned]</c>-marked class and a BCL type (<see cref="Uri"/>)
    ///     are kept because neither counts as a navigation reference.
    /// </summary>
    [Fact]
    public void KitchenSinkEntity_DefaultIgnoreComplexType_KeepsScalarsAndOwnedAndFrameworkTypes_DropsPlainNavigation()
    {
        // Arrange
        const string entitySource = """
            public sealed class GlobalWidget
            {
                public string Label { get; set; } = string.Empty;
            }

            namespace Probe.Entities
            {
                public sealed class Address
                {
                    public string City { get; set; } = string.Empty;
                }

                [System.AttributeUsage(System.AttributeTargets.Class)]
                public sealed class OwnedAttribute : System.Attribute
                {
                }

                [Owned]
                public sealed class OwnedThing
                {
                    public string Label { get; set; } = string.Empty;
                }

                public sealed class KitchenSink
                {
                    public bool BoolProp { get; set; }
                    public char CharProp { get; set; }
                    public sbyte SByteProp { get; set; }
                    public byte ByteProp { get; set; }
                    public short ShortProp { get; set; }
                    public ushort UShortProp { get; set; }
                    public int IntProp { get; set; }
                    public uint UIntProp { get; set; }
                    public long LongProp { get; set; }
                    public ulong ULongProp { get; set; }
                    public float FloatProp { get; set; }
                    public double DoubleProp { get; set; }
                    public decimal DecimalProp { get; set; }
                    public object ObjProp { get; set; } = new object();
                    public int? NullableIntProp { get; set; }
                    public int[] ArrayProp { get; set; } = System.Array.Empty<int>();
                    public System.Collections.Generic.List<string> ListProp { get; set; } = new System.Collections.Generic.List<string>();
                    public System.Collections.Generic.IEnumerable<string> EnumerableProp { get; set; } = new System.Collections.Generic.List<string>();
                    public Address RequiredAddress { get; set; } = new Address();
                    public Address[] AddressArray { get; set; } = System.Array.Empty<Address>();
                    public OwnedThing OwnedProp { get; set; } = new OwnedThing();
                    public System.Uri? SiteUri { get; set; }
                    public GlobalWidget GlobalProp { get; set; } = new GlobalWidget();
                }
            }
            """;
        const string dtoSource = """
            using DKNet.EfCore.DtoGenerator;

            namespace Probe.Dtos
            {
                [GenerateDto(typeof(Probe.Entities.KitchenSink))]
                public partial record KitchenSinkDto;
            }
            """;

        // Act
        var (_, generatedSources) = RunGenerator(entitySource, dtoSource);
        var source = string.Join('\n', generatedSources.Select(s => s.SourceText.ToString()));

        // Assert: every primitive keyword rendered, not the CLR type name
        source.ShouldContain("bool BoolProp");
        source.ShouldContain("char CharProp");
        source.ShouldContain("sbyte SByteProp");
        source.ShouldContain("byte ByteProp");
        source.ShouldContain("short ShortProp");
        source.ShouldContain("ushort UShortProp");
        source.ShouldContain("int IntProp");
        source.ShouldContain("uint UIntProp");
        source.ShouldContain("long LongProp");
        source.ShouldContain("ulong ULongProp");
        source.ShouldContain("float FloatProp");
        source.ShouldContain("double DoubleProp");
        source.ShouldContain("decimal DecimalProp");
        source.ShouldContain("object ObjProp");
        source.ShouldContain("int? NullableIntProp");
        source.ShouldContain("int[] ArrayProp");
        source.ShouldContain("List<string> ListProp");
        source.ShouldContain("IEnumerable<string> EnumerableProp");

        // Assert: owned type and BCL type are kept (neither is a navigation reference)
        source.ShouldContain("OwnedThing OwnedProp");
        source.ShouldContain("Uri? SiteUri");

        // Assert: plain navigation references (single, array, and a global-namespace class) are dropped
        source.ShouldNotContain("RequiredAddress");
        source.ShouldNotContain("AddressArray");
        source.ShouldNotContain("GlobalProp");
    }

    #endregion

    #region Internals

    private static (List<Diagnostic> Diagnostics, List<GeneratedSourceResult> GeneratedSources) RunGenerator(
        string entitySource, string dtoSource)
    {
        var compilation = CSharpCompilation.Create(
            "ProbeCompilation",
            [
                CSharpSyntaxTree.ParseText(GenerateDtoAttributeSource),
                CSharpSyntaxTree.ParseText(entitySource, path: "Entity.cs"),
                CSharpSyntaxTree.ParseText(dtoSource, path: "Dto.cs"),
            ],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DKNet.EfCore.DtoGenerator.DtoGenerator().AsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator], optionsProvider: new TestAnalyzerConfigOptionsProvider());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var runResult = ((CSharpGeneratorDriver)driver).GetRunResult();

        return (
            runResult.Results.SelectMany(r => r.Diagnostics).ToList(),
            runResult.Results.SelectMany(r => r.GeneratedSources).ToList());
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
