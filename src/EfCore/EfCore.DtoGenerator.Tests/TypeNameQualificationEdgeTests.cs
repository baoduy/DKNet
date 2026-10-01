// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EfCore.DtoGenerator.Tests;

/// <summary>
///     Edge cases of the DRK-1905 type-name rendering beyond the frozen acceptance tests in
///     <see cref="TypeNameQualificationTests"/>: collision keys come from the outermost containing type,
///     qualification applies inside array elements and <c>Nullable&lt;T&gt;</c>, a colliding generic is qualified before
///     the generic branch runs, and a nested type inside a generic containing type renders fully qualified. A
///     bare name also qualifies when a namespace the generated file imports declares a different accessible type
///     with the same (name, arity), but not when that type is inaccessible or the DTO's enclosing namespace
///     chain declares the name first.
/// </summary>
public class TypeNameQualificationEdgeTests
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

    private const string TwoNestedTypesEntitySource = """
        namespace Probe.Entities
        {
            public class Order
            {
                public enum Priority { Low, High }

                public enum Kind { Retail, Wholesale }

                public struct Slot<T> { }

                public int Id { get; set; }

                public Priority Urgency { get; set; }

                public Kind Category { get; set; }

                public Slot<int> Window { get; set; }
            }
        }
        """;

    private const string NestedUnderCollidingOuterEntitySource = """
        namespace Probe.Billing
        {
            public class Order
            {
                public enum Code { None, Refund }
            }
        }

        namespace Probe.Entities
        {
            public class Order
            {
                public enum Priority { Low, High }

                public int Id { get; set; }

                public Priority Urgency { get; set; }

                public Probe.Billing.Order.Code BillingCode { get; set; }
            }
        }
        """;

    private const string CollisionInArrayEntitySource = """
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

                public Probe.Billing.Status[] BillingHistory { get; set; } = [];

                public Probe.Shipping.Status ShippingStatus { get; set; }
            }
        }
        """;

    private const string CollisionInNullableEntitySource = """
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

                public Probe.Billing.Status? PendingBilling { get; set; }

                public Probe.Shipping.Status ShippingStatus { get; set; }
            }
        }
        """;

    private const string CollidingGenericEntitySource = """
        namespace Probe.Billing
        {
            public struct Box<T> { }
        }

        namespace Probe.Shipping
        {
            public struct Box<T> { }
        }

        namespace Probe.Entities
        {
            public class Order
            {
                public int Id { get; set; }

                public Probe.Billing.Box<int> BillingBox { get; set; }

                public Probe.Shipping.Box<int> ShippingBox { get; set; }
            }
        }
        """;

    private const string SameGenericDefinitionEntitySource = """
        using System.Collections.Generic;

        namespace Probe.Entities
        {
            public class Order
            {
                public int Id { get; set; }

                public List<int> Scores { get; set; } = [];

                public List<string> Tags { get; set; } = [];
            }
        }
        """;

    private const string DynamicPropertyEntitySource = """
        namespace Probe.Entities
        {
            public class Order
            {
                public int Id { get; set; }

                public dynamic Payload { get; set; } = 0;
            }
        }
        """;

    private const string NestedInGenericContainerEntitySource = """
        namespace Probe.Entities
        {
            public class Outer<T>
            {
                public enum Inner { First, Second }
            }

            public class Order
            {
                public int Id { get; set; }

                public Outer<int>.Inner Mode { get; set; }
            }
        }
        """;

    private const string ImportedOuterTypeCollisionEntitySource = """
        namespace Probe.Billing
        {
            public class Order { }

            public enum Currency { Usd }
        }

        namespace Probe.Entities
        {
            public class Order
            {
                public enum Priority { Low, High }

                public int Id { get; set; }

                public Priority Urgency { get; set; }

                public Probe.Billing.Currency Currency { get; set; }
            }
        }
        """;

    private const string ImportedTypeCollisionEntitySource = """
        namespace Probe.Billing
        {
            public enum Status { Unpaid, Paid }

            public enum Currency { Usd }
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

                public Probe.Billing.Currency Currency { get; set; }

                public Probe.Shipping.Status ShippingStatus { get; set; }
            }
        }
        """;

    private const string InternalImportedTypeEntitySource = """
        namespace Probe.Billing
        {
            internal enum Status { Unpaid, Paid }

            public enum Currency { Usd }
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

                public Probe.Billing.Currency Currency { get; set; }

                public Probe.Shipping.Status ShippingStatus { get; set; }
            }
        }
        """;

    private const string BillingLibraryPublicStatusSource = """
        namespace Probe.Billing
        {
            public enum Status { Unpaid, Paid }

            public enum Currency { Usd }
        }
        """;

    private const string BillingLibraryInternalStatusSource = """
        namespace Probe.Billing
        {
            internal enum Status { Unpaid, Paid }

            public enum Currency { Usd }
        }
        """;

    private const string ShippingWithReferencedBillingEntitySource = """
        namespace Probe.Shipping
        {
            public enum Status { Pending, Shipped }
        }

        namespace Probe.Entities
        {
            public class Order
            {
                public int Id { get; set; }

                public Probe.Billing.Currency Currency { get; set; }

                public Probe.Shipping.Status ShippingStatus { get; set; }
            }
        }
        """;

    private const string EnclosingNamespaceTypeEntitySource = """
        namespace Probe
        {
            public enum Status { Draft, Final }
        }

        namespace Probe.Billing
        {
            public enum Status { Unpaid, Paid }

            public enum Currency { Usd }
        }

        namespace Probe.Entities
        {
            public class Order
            {
                public int Id { get; set; }

                public Probe.Billing.Currency Currency { get; set; }

                public Probe.Status Kind { get; set; }
            }
        }
        """;

    private const string GlobalNamespaceTypeEntitySource = """
        public enum Status { Draft, Final }

        namespace Probe.Billing
        {
            public enum Status { Unpaid, Paid }

            public enum Currency { Usd }
        }

        namespace Probe.Entities
        {
            public class Order
            {
                public int Id { get; set; }

                public Probe.Billing.Currency Currency { get; set; }

                public global::Status Kind { get; set; }
            }
        }
        """;

    private const string DtoNamespaceTypeEntitySource = """
        namespace Probe.Dtos
        {
            public enum Status { Draft, Final }
        }

        namespace Probe.Billing
        {
            public enum Status { Unpaid, Paid }

            public enum Currency { Usd }
        }

        namespace Probe.Entities
        {
            public class Order
            {
                public int Id { get; set; }

                public Probe.Billing.Currency Currency { get; set; }

                public Probe.Dtos.Status Kind { get; set; }
            }
        }
        """;

    private const string UnimportedCollisionInArrayEntitySource = """
        namespace Probe.Billing
        {
            public enum Index { First, Last }
        }

        namespace Probe.Entities
        {
            public class Order
            {
                public int Id { get; set; }

                public System.Index[] Cursors { get; set; } = [];

                public Probe.Billing.Index Position { get; set; }
            }
        }
        """;

    private const string UnimportedCollisionInGenericEntitySource = """
        using System.Collections.Generic;

        namespace Probe.Billing
        {
            public enum Index { First, Last }
        }

        namespace Probe.Entities
        {
            public class Order
            {
                public int Id { get; set; }

                public List<System.Index> Cursors { get; set; } = [];

                public Probe.Billing.Index Position { get; set; }
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

    private const string GlobalNamespaceOrderDtoSource = """
        using DKNet.EfCore.DtoGenerator;

        [GenerateDto(typeof(Probe.Entities.Order))]
        public partial record OrderDto;
        """;

    private static readonly MetadataReference[] References =
    [
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(ImmutableArray<>).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.DynamicAttribute).Assembly.Location),
    ];

    #endregion

    #region Methods

    [Fact]
    public void TwoNestedTypesUnderSameOuterType_GeneratedSource_KeepsContainingTypeChainWithoutGlobal()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(TwoNestedTypesEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public Order.Priority Urgency { get; init; }");
        source.ShouldContain("public Order.Kind Category { get; init; }");
        source.ShouldContain("public Order.Slot<int> Window { get; init; }");
        source.ShouldNotContain("global::");
    }

    [Fact]
    public void NestedTypesUnderTwoSameNamedOuterTypes_GeneratedSource_QualifiesBothFully()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(NestedUnderCollidingOuterEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public global::Probe.Entities.Order.Priority Urgency { get; init; }");
        source.ShouldContain("public global::Probe.Billing.Order.Code BillingCode { get; init; }");
    }

    [Fact]
    public void CollisionInsideArrayElement_GeneratedSource_QualifiesElementType()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(CollisionInArrayEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public global::Probe.Billing.Status[] BillingHistory { get; init; }");
        source.ShouldContain("public global::Probe.Shipping.Status ShippingStatus { get; init; }");
    }

    [Fact]
    public void CollisionInsideNullable_GeneratedSource_QualifiesUnderlyingType()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(CollisionInNullableEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public global::Probe.Billing.Status? PendingBilling { get; init; }");
        source.ShouldContain("public global::Probe.Shipping.Status ShippingStatus { get; init; }");
    }

    [Fact]
    public void SameGenericNameAndArityFromTwoNamespaces_GeneratedSource_QualifiesBothFully()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(CollidingGenericEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public global::Probe.Billing.Box<int> BillingBox { get; init; }");
        source.ShouldContain("public global::Probe.Shipping.Box<int> ShippingBox { get; init; }");
    }

    [Fact]
    public void NestedTypeInsideGenericContainingType_GeneratedSource_RendersFullyQualified()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(NestedInGenericContainerEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public global::Probe.Entities.Outer<int>.Inner Mode { get; init; }");
    }

    [Fact]
    public void SameGenericDefinitionWithDifferentTypeArguments_GeneratedSource_KeepsBothBare()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(SameGenericDefinitionEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public List<int> Scores { get; init; }");
        source.ShouldContain("public List<string> Tags { get; init; }");
        source.ShouldNotContain("global::");
    }

    [Fact]
    public void DynamicProperty_GeneratedSource_KeepsDynamicKeyword()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(DynamicPropertyEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public dynamic Payload { get; init; }");
    }

    [Fact]
    public void NestedTypeWhoseOuterNameAnImportedNamespaceAlsoDeclares_GeneratedSource_QualifiesFully()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(ImportedOuterTypeCollisionEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public global::Probe.Entities.Order.Priority Urgency { get; init; }");
        source.ShouldContain("public Currency Currency { get; init; }");
    }

    [Fact]
    public void SimpleNameAnImportedNamespaceAlsoDeclares_GeneratedSource_QualifiesFully()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(ImportedTypeCollisionEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public global::Probe.Shipping.Status ShippingStatus { get; init; }");
        source.ShouldContain("public Currency Currency { get; init; }");
    }

    [Fact]
    public void SimpleNameAnImportedNamespaceDeclaresInternallyInSameAssembly_GeneratedSource_QualifiesFully()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(InternalImportedTypeEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public global::Probe.Shipping.Status ShippingStatus { get; init; }");
    }

    [Fact]
    public void SimpleNameAnImportedNamespaceDeclaresPublicInReferencedAssembly_GeneratedSource_QualifiesFully()
    {
        // Arrange
        var billing = CreateLibraryReference(BillingLibraryPublicStatusSource);

        // Act
        var (source, errors) = CompileAndCaptureSource(ShippingWithReferencedBillingEntitySource, extraReferences: billing);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public global::Probe.Shipping.Status ShippingStatus { get; init; }");
    }

    [Fact]
    public void SimpleNameAnImportedNamespaceDeclaresInternalInReferencedAssembly_GeneratedSource_KeepsBareName()
    {
        // Arrange
        var billing = CreateLibraryReference(BillingLibraryInternalStatusSource);

        // Act
        var (source, errors) = CompileAndCaptureSource(ShippingWithReferencedBillingEntitySource, extraReferences: billing);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public Status ShippingStatus { get; init; }");
        source.ShouldNotContain("global::");
    }

    [Fact]
    public void SimpleNameTheDtoEnclosingNamespaceDeclares_GeneratedSource_KeepsBareName()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(EnclosingNamespaceTypeEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public Status Kind { get; init; }");
        source.ShouldNotContain("global::");
    }

    [Fact]
    public void SimpleNameAnImportedNamespaceAlsoDeclaresForGlobalNamespaceDto_GeneratedSource_QualifiesFully()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(
            ImportedTypeCollisionEntitySource, dtoSource: GlobalNamespaceOrderDtoSource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public global::Probe.Shipping.Status ShippingStatus { get; init; }");
    }

    [Fact]
    public void SimpleNameTheGlobalNamespaceDeclares_GeneratedSource_KeepsBareName()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(GlobalNamespaceTypeEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public Status Kind { get; init; }");
        source.ShouldNotContain("global::");
    }

    [Fact]
    public void SimpleNameTheDtoOwnNamespaceDeclares_GeneratedSource_KeepsBareName()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(DtoNamespaceTypeEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public Status Kind { get; init; }");
        source.ShouldNotContain("global::");
    }

    [Fact]
    public void CollisionWithUnimportedNamespaceInsideArrayElement_GeneratedSource_QualifiesBothFully()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(UnimportedCollisionInArrayEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public global::System.Index[] Cursors { get; init; }");
        source.ShouldContain("public global::Probe.Billing.Index Position { get; init; }");
    }

    [Fact]
    public void CollisionWithUnimportedNamespaceInsideGenericArgument_GeneratedSource_QualifiesBothFully()
    {
        // Arrange + Act
        var (source, errors) = CompileAndCaptureSource(UnimportedCollisionInGenericEntitySource);

        // Assert
        errors.ShouldBeEmpty(string.Join('\n', errors.Select(d => d.ToString())));
        source.ShouldContain("public List<global::System.Index> Cursors { get; init; }");
        source.ShouldContain("public global::Probe.Billing.Index Position { get; init; }");
    }

    #endregion

    #region Internals

    /// <summary>
    /// Runs <c>DtoGenerator</c> over <paramref name="entitySource"/> plus <paramref name="dtoSource"/>
    /// (<see cref="OrderDtoSource"/> when omitted) and
    /// returns the generated source with the error diagnostics located in the generator's own output trees.
    /// The hand-authored input trees are asserted error-free first, so a probe typo cannot surface as an
    /// error type the generator renders silently. <paramref name="extraReferences"/> are added to
    /// <see cref="References"/>.
    /// </summary>
    private static (string Source, List<Diagnostic> Errors) CompileAndCaptureSource(
        string entitySource, string dtoSource = OrderDtoSource, params MetadataReference[] extraReferences)
    {
        var compilation = CSharpCompilation.Create(
            "ProbeCompilation",
            [
                CSharpSyntaxTree.ParseText(GenerateDtoAttributeSource),
                CSharpSyntaxTree.ParseText(entitySource),
                CSharpSyntaxTree.ParseText(dtoSource),
            ],
            [.. References, .. extraReferences],
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

    /// <summary>
    /// Compiles <paramref name="librarySource"/> into a separate assembly and returns a reference to it.
    /// </summary>
    private static MetadataReference CreateLibraryReference(string librarySource)
    {
        var library = CSharpCompilation.Create(
            "ProbeBillingLibrary",
            [CSharpSyntaxTree.ParseText(librarySource)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var libraryErrors = library.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        libraryErrors.ShouldBeEmpty(string.Join('\n', libraryErrors.Select(d => d.ToString())));

        return library.ToMetadataReference();
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
