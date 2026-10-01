// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EfCore.DtoGenerator.Tests;

/// <summary>
///     In-process generator-driver tests pinning how <c>DtoGenerator</c> renders property type names
///     (DRK-1905): a nested type renders with its containing-type chain (CS0246 today), a simple name shared
///     by two distinct property types renders fully qualified with <c>global::</c> (CS0104 today), and every
///     shape that compiles today keeps its bare rendering. Probe entities live in namespaces other than the
///     DTO's (<c>Probe.Entities</c>, <c>Probe.Billing</c>, <c>Probe.Shipping</c>, <c>Probe.Ui</c> vs
///     <c>Probe.Dtos</c>) so the generator's <c>using</c> path is exercised. Every probe type is an enum so no
///     property is dropped as a complex navigation.
/// </summary>
public class TypeNameQualificationTests
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

    private const string NestedTypeEntitySource = """
        namespace Probe.Entities
        {
            public class Order
            {
                public enum Priority { Low, High }

                public int Id { get; set; }

                public Priority Urgency { get; set; }

                public Priority? FallbackUrgency { get; set; }
            }
        }
        """;

    private const string CollidingNamesEntitySource = """
        namespace Probe.Billing
        {
            public enum Status { Unpaid, Paid }
        }

        namespace Probe.Shipping
        {
            public enum Status { Pending, Shipped }
        }

        namespace Probe.Entities
        {
            public class Order
            {
                public int Id { get; set; }

                public Probe.Billing.Status BillingStatus { get; set; }

                public Probe.Shipping.Status ShippingStatus { get; set; }
            }
        }
        """;

    private const string CollisionInsideGenericEntitySource = """
        using System.Collections.Generic;

        namespace Probe.Billing
        {
            public enum Status { Unpaid, Paid }
        }

        namespace Probe.Shipping
        {
            public enum Status { Pending, Shipped }
        }

        namespace Probe.Entities
        {
            public class Order
            {
                public int Id { get; set; }

                public List<Probe.Billing.Status> History { get; set; } = [];

                public Probe.Shipping.Status ShippingStatus { get; set; }
            }
        }
        """;

    private const string SingleForeignTypeEntitySource = """
        namespace Probe.Billing
        {
            public enum Status { Unpaid, Paid }
        }

        namespace Probe.Entities
        {
            public class Order
            {
                public int Id { get; set; }

                public Probe.Billing.Status Status { get; set; }
            }
        }
        """;

    private const string DifferentArityEntitySource = """
        using System.Collections.Generic;

        namespace Probe.Ui
        {
            public enum List { Compact, Wide }
        }

        namespace Probe.Entities
        {
            public class Order
            {
                public int Id { get; set; }

                public List<string> Tags { get; set; } = [];

                public Probe.Ui.List Panel { get; set; }
            }
        }
        """;

    private const string OrderDtoSource = """
        using DKNet.EfCore.DtoGenerator;

        namespace Probe.Dtos
        {
            [GenerateDto(typeof(Probe.Entities.Order))]
            public partial record OrderDto;
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
    public void NestedTypeProperty_GeneratedSource_CompilesAndRendersContainingTypeChain()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(NestedTypeEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public Order.Priority Urgency { get; init; }");
        source.ShouldContain("public Order.Priority? FallbackUrgency { get; init; }");
    }

    [Fact]
    public void SameSimpleNameFromTwoNamespaces_GeneratedSource_CompilesAndRendersBothFullyQualified()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(CollidingNamesEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public global::Probe.Billing.Status BillingStatus { get; init; }");
        source.ShouldContain("public global::Probe.Shipping.Status ShippingStatus { get; init; }");
    }

    [Fact]
    public void SameSimpleNameInsideGenericArgument_GeneratedSource_CompilesAndQualifiesAtEveryDepth()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(CollisionInsideGenericEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        // R1 + R4: the colliding Status is qualified inside the generic argument; List<T> itself has no
        // colliding (name, arity) and stays bare.
        source.ShouldContain("public List<global::Probe.Billing.Status> History { get; init; }");
        source.ShouldContain("public global::Probe.Shipping.Status ShippingStatus { get; init; }");
    }

    [Fact]
    public void SingleNonCollidingTypeFromOtherNamespace_GeneratedSource_KeepsBareSimpleName()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(SingleForeignTypeEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public Status Status { get; init; }");
        source.ShouldNotContain("global::Probe.Billing.Status");
        source.ShouldNotContain("Billing.Status Status");
    }

    [Fact]
    public void SameSimpleNameWithDifferentArity_GeneratedSource_KeepsBothBare()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(DifferentArityEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public List<string> Tags { get; init; }");
        source.ShouldContain("public List Panel { get; init; }");
        source.ShouldNotContain("global::Probe.Ui.List");
        source.ShouldNotContain("global::System.Collections.Generic.List");
    }

    #endregion

    #region Internals

    /// <summary>
    /// Runs <c>DtoGenerator</c> over <paramref name="entitySource"/> plus <see cref="OrderDtoSource"/> and
    /// returns the generated source with the error diagnostics located in the generator's own output trees.
    /// The hand-authored input trees are asserted error-free first, so a probe typo cannot surface as an
    /// error type the generator renders silently.
    /// </summary>
    private static (string Source, List<Diagnostic> Errors) CompileAndCaptureSource(string entitySource)
    {
        var compilation = CSharpCompilation.Create(
            "ProbeCompilation",
            [
                CSharpSyntaxTree.ParseText(GenerateDtoAttributeSource),
                CSharpSyntaxTree.ParseText(entitySource),
                CSharpSyntaxTree.ParseText(OrderDtoSource),
            ],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var inputErrors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        inputErrors.ShouldBeEmpty(string.Join('\n', inputErrors.Select(d => d.ToString())));

        var generator = new DKNet.EfCore.DtoGenerator.DtoGenerator().AsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator], optionsProvider: new TestAnalyzerConfigOptionsProvider());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation, out _);
        var runResult = ((CSharpGeneratorDriver)driver).GetRunResult();

        var generatedSource = string.Join('\n', runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString()));
        generatedSource.ShouldNotBeNullOrEmpty("generator should have produced DTO source");

        var generatedTrees = runResult.GeneratedTrees.ToHashSet();
        var generatedErrors = updatedCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error &&
                        d.Location.SourceTree is not null &&
                        generatedTrees.Contains(d.Location.SourceTree))
            .ToList();

        return (generatedSource, generatedErrors);
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
