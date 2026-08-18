using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;

namespace EfCore.DtoGenerator.Tests;

/// <summary>
///     DRK-450 acceptance criteria — <c>@unit</c> scenarios for the <c>[RaisesEvent]</c> build-time
///     validator, driven in-process against an in-memory compilation exactly like
///     <see cref="DtoGeneratorPrecedenceTests" />. None of these compilations reference
///     <c>DKNet.EfCore.Events</c> — the validator emits diagnostics only from the entity/payload syntax,
///     proving a domain project can carry rules without the event runtime present.
/// </summary>
public class RaisesEventDiagnosticsTests
{
    #region Constants

    private const string RuntimeContractSource = """
        using System;

        namespace DKNet.EfCore.Abstractions.Events
        {
            [Flags]
            public enum EventOperations
            {
                Created = 1,
                Updated = 2,
                Deleted = 4,
            }

            [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
            public sealed class RaisesEventAttribute : Attribute
            {
                public RaisesEventAttribute(Type eventType, EventOperations operations, params string[] properties)
                {
                    EventType = eventType;
                    Operations = operations;
                    Properties = properties;
                }

                public Type EventType { get; }
                public EventOperations Operations { get; }
                public string[] Properties { get; }
            }
        }
        """;

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

    private const string EntitySource = """
        using System;
        using DKNet.EfCore.Abstractions.Events;
        using DKNet.EfCore.DtoGenerator;

        namespace Probe.Entities
        {
            public sealed class DeliveryAddress
            {
                public string City { get; set; } = string.Empty;
            }

            [GenerateDto(typeof(Order))]
            public partial record OrderPlacedEvent;

            [GenerateDto(typeof(Customer))]
            public partial record CustomerRegisteredEvent;

            {{Declaration}}
            public sealed class Order
            {
                public Guid Id { get; set; }
                public string Status { get; set; } = string.Empty;
                public string DeliveryNote { get; set; } = string.Empty;
                public DeliveryAddress? DeliveryAddress { get; set; }
            }

            public sealed class Customer
            {
                public Guid Id { get; set; }
                public string Name { get; set; } = string.Empty;
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
    public void RuleReferencingUnknownProperty_FailsTheBuildNamingTheProperty()
    {
        // Arrange
        const string declaration = """[RaisesEvent(typeof(OrderPlacedEvent), EventOperations.Updated, "Statuz")]""";

        // Act
        var result = RunGenerator(declaration);

        // Assert
        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "DKRAISEVT001" && d.Severity == DiagnosticSeverity.Error);
        error.ShouldNotBeNull();
        error.GetMessage().ShouldContain("Statuz");
    }

    [Fact]
    public void RuleReferencingNestedPath_FailsTheBuildNamingTheUnsupportedPath()
    {
        // Arrange - a literal dotted string, not nameof: nameof cannot express a nested path
        const string declaration = """[RaisesEvent(typeof(OrderPlacedEvent), EventOperations.Updated, "DeliveryAddress.City")]""";

        // Act
        var result = RunGenerator(declaration);

        // Assert
        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "DKRAISEVT001" && d.Severity == DiagnosticSeverity.Error);
        error.ShouldNotBeNull();
        error.GetMessage().ShouldContain("nested path");
    }

    [Fact]
    public void RuleNamingAPayloadFromADifferentEntity_FailsTheBuildNamingTheMismatch()
    {
        // Arrange - CustomerRegisteredEvent is generated from Customer, not Order
        const string declaration = """[RaisesEvent(typeof(CustomerRegisteredEvent), EventOperations.Created)]""";

        // Act
        var result = RunGenerator(declaration);

        // Assert
        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "DKRAISEVT002" && d.Severity == DiagnosticSeverity.Error);
        error.ShouldNotBeNull();
        error.GetMessage().ShouldContain("Customer");
    }

    [Fact]
    public void NarrowingOnARuleWithNoUpdateOperation_WarnsAndStillBuilds()
    {
        // Arrange - narrowing means nothing on a create-only rule
        const string declaration = """[RaisesEvent(typeof(OrderPlacedEvent), EventOperations.Created, "Status")]""";

        // Act
        var result = RunGenerator(declaration);

        // Assert
        result.Diagnostics.ShouldNotContain(d => d.Severity == DiagnosticSeverity.Error);
        var warning = result.Diagnostics.FirstOrDefault(d => d.Id == "DKRAISEVT003" && d.Severity == DiagnosticSeverity.Warning);
        warning.ShouldNotBeNull();
    }

    [Fact]
    public void DomainProjectCarryingARule_BuildsCleanly_WithNoEventRuntimeReferenced()
    {
        // Arrange - a well-formed rule; note References above never include DKNet.EfCore.Events
        const string declaration = """[RaisesEvent(typeof(OrderPlacedEvent), EventOperations.Created)]""";

        // Act
        var result = RunGenerator(declaration);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        result.CompilationDiagnostics.ShouldNotContain(d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ProjectDeclaringNoRule_NeedsNoEventPackage_ValidatorProducesNothing()
    {
        // Arrange - a plain [GenerateDto] payload with no [RaisesEvent] anywhere in the compilation
        const string declaration = "";

        // Act
        var result = RunGenerator(declaration);

        // Assert - the validator is silent; it has nothing to check and adds no new authoring surface
        result.Diagnostics.ShouldBeEmpty();
        result.CompilationDiagnostics.ShouldNotContain(d => d.Severity == DiagnosticSeverity.Error);
    }

    private static GeneratorOutput RunGenerator(string declaration)
    {
        var entitySource = EntitySource.Replace("{{Declaration}}", declaration);

        var compilation = CSharpCompilation.Create(
            "ProbeCompilation",
            [
                CSharpSyntaxTree.ParseText(RuntimeContractSource),
                CSharpSyntaxTree.ParseText(GenerateDtoAttributeSource),
                CSharpSyntaxTree.ParseText(entitySource),
            ],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DKNet.EfCore.DtoGenerator.RaisesEventValidator().AsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create([generator]);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        var runResult = ((CSharpGeneratorDriver)driver).GetRunResult();

        return new GeneratorOutput(
            runResult.Results.SelectMany(r => r.Diagnostics).ToList(),
            outputCompilation.GetDiagnostics().ToList());
    }

    private sealed record GeneratorOutput(List<Diagnostic> Diagnostics, List<Diagnostic> CompilationDiagnostics);

    #endregion
}
