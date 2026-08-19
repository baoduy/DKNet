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

                public RaisesEventAttribute(string eventName, EventOperations operations, params string[] properties)
                {
                    EventName = eventName;
                    Operations = operations;
                    Properties = properties;
                }

                public Type? EventType { get; }
                public string? EventName { get; }
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

    [Fact]
    public void ProjectGeneratingPayloads_DeclaringNoRule_ProducesTheUnchangedDefaultShapePayload()
    {
        // Arrange - concrete baseline (spec-review nit): OrderPlacedEvent is a plain [GenerateDto] payload,
        // Order carries no [RaisesEvent] rule at all. Assert the exact members generated, not "unchanged".
        const string declaration = "";

        // Act - both generators run together, exactly as they do in a real build
        var result = RunBothGenerators(declaration);

        // Assert - DKDTOGEN003 is DtoGenerator's routine "N properties excluded" info notice, unrelated
        // to [RaisesEvent]; the build must still be free of errors and warnings.
        result.Diagnostics.ShouldNotContain(d => d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Warning);
        result.CompilationDiagnostics.ShouldNotContain(d => d.Severity == DiagnosticSeverity.Error);
        result.GeneratedSources.ShouldNotContain(s => s.HintName.Contains("RaisesEvent.g.cs", StringComparison.Ordinal));

        var payloadSource = result.GeneratedSources
            .Single(s => s.HintName.Contains("OrderPlacedEvent", StringComparison.Ordinal))
            .SourceText.ToString();
        payloadSource.ShouldContain("Id");
        payloadSource.ShouldContain("Status");
        payloadSource.ShouldContain("DeliveryNote");
        payloadSource.ShouldNotContain("DeliveryAddress"); // complex/nav property, excluded by default
    }

    [Fact]
    public void StringFormName_ResolvingToAnIncompatibleExistingType_FailsTheBuild()
    {
        // Arrange - LoyaltyMembershipEvents is a plain (non-partial, non-record) type already declared;
        // naming it by string must fail rather than silently reuse or collide with it.
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Probe.Entities
            {
                public sealed class LoyaltyMembershipEvents
                {
                    public int Points { get; set; }
                }

                [RaisesEvent("LoyaltyMembershipEvents", EventOperations.Created)]
                public sealed class LoyaltyMembership
                {
                    public Guid Id { get; set; }
                    public int Points { get; set; }
                }
            }
            """;

        // Act
        var result = RunGeneratorWithSource(source);

        // Assert
        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "DKRAISEVT004" && d.Severity == DiagnosticSeverity.Error);
        error.ShouldNotBeNull();
        error.GetMessage().ShouldContain("LoyaltyMembershipEvents");
        error.GetMessage().ShouldContain("typeof(LoyaltyMembershipEvents)");
    }

    [Fact]
    public void StringFormName_NotACompileTimeConstant_FailsTheBuild()
    {
        // Arrange - the name is read from a non-const static field, not written as a literal
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Probe.Entities
            {
                public static class EventNames
                {
                    public static string OrderTouched = "OrderTouched";
                }

                [RaisesEvent(EventNames.OrderTouched, EventOperations.Created)]
                public sealed class Order
                {
                    public Guid Id { get; set; }
                }
            }
            """;

        // Act
        var result = RunGeneratorWithSource(source);

        // Assert
        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "DKRAISEVT005" && d.Severity == DiagnosticSeverity.Error);
        error.ShouldNotBeNull();
        error.GetMessage().ShouldContain("Order");
        result.GeneratedSources.ShouldNotContain(s => s.HintName.Contains("RaisesEvent.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void StringFormName_NotASingleIdentifier_FailsTheBuildNamingIt()
    {
        // Arrange
        const string declaration = """[RaisesEvent("Foo.Bar", EventOperations.Created)]""";

        // Act
        var result = RunGenerator(declaration);

        // Assert
        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "DKRAISEVT005" && d.Severity == DiagnosticSeverity.Error);
        error.ShouldNotBeNull();
        error.GetMessage().ShouldContain("Foo.Bar");
    }

    [Fact]
    public void TwoEntitiesInOneNamespace_NamingTheSameStringEvent_FailsTheBuildIdentifyingBoth()
    {
        // Arrange
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Domain.Sales
            {
                [RaisesEvent("RecordTouched", EventOperations.Created)]
                public sealed class Order
                {
                    public Guid Id { get; set; }
                }

                [RaisesEvent("RecordTouched", EventOperations.Created)]
                public sealed class Invoice
                {
                    public Guid Id { get; set; }
                }
            }
            """;

        // Act
        var result = RunGeneratorWithSource(source);

        // Assert
        var errors = result.Diagnostics.Where(d => d.Id == "DKRAISEVT006" && d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Count.ShouldBe(2); // one per declaration, each identifying both entities
        errors.ShouldAllBe(d => d.GetMessage().Contains("Order") && d.GetMessage().Contains("Invoice"));
        result.GeneratedSources.ShouldNotContain(s => s.HintName.Contains("RecordTouched", StringComparison.Ordinal));
    }

    [Fact]
    public void NamingANonexistentType_IsStillACompileError_NeverAGenerationTrigger()
    {
        // Arrange - LoyaltyMembershipEvnets (typo) does not exist; the type-naming form never falls back
        // to generation, unlike the string form.
        const string declaration = """[RaisesEvent(typeof(LoyaltyMembershipEvnets), EventOperations.Created)]""";

        // Act
        var result = RunGenerator(declaration);

        // Assert - the native compiler rejects the unresolved type (CS0246); the validator resolves it to
        // an error-type symbol and, correctly, still reports it as a mismatched payload rather than
        // silently accepting it — either way the build fails and nothing is generated for the typo'd name.
        result.CompilationDiagnostics.ShouldContain(d => d.Severity == DiagnosticSeverity.Error);
        result.Diagnostics.ShouldNotContain(d =>
            d.Id == "DKRAISEVT004" || d.Id == "DKRAISEVT005" || d.Id == "DKRAISEVT006");
        result.GeneratedSources.ShouldNotContain(s => s.HintName.Contains("LoyaltyMembershipEvnets", StringComparison.Ordinal));
    }

    [Fact]
    public void NarrowingAStringFormRule_ToAPropertyTheEntityDoesNotHave_FailsTheBuild()
    {
        // Arrange - narrowing validation is identical for both forms; exercised here for the string form
        const string declaration = """[RaisesEvent("OrderTouched", EventOperations.Updated, "Sttaus")]""";

        // Act
        var result = RunGenerator(declaration);

        // Assert
        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "DKRAISEVT001" && d.Severity == DiagnosticSeverity.Error);
        error.ShouldNotBeNull();
        error.GetMessage().ShouldContain("Sttaus");
    }

    [Fact]
    public void NarrowingOnAStringFormCreateOnlyRule_WarnsExactlyAsTheExistingFormDoes()
    {
        // Arrange - narrowing means nothing on a create-only rule, string form or type form alike
        const string declaration = """[RaisesEvent("OrderTouched", EventOperations.Created, "Status")]""";

        // Act
        var result = RunGenerator(declaration);

        // Assert
        result.Diagnostics.ShouldNotContain(d => d.Severity == DiagnosticSeverity.Error);
        var warning = result.Diagnostics.FirstOrDefault(d => d.Id == "DKRAISEVT003" && d.Severity == DiagnosticSeverity.Warning);
        warning.ShouldNotBeNull();
    }

    [Fact]
    public void ValidStringFormDeclaration_GeneratesAPublicPartialRecord_WithTheEntitysDefaultShapeMembers()
    {
        // Arrange
        const string declaration = """[RaisesEvent("OrderTouched", EventOperations.Created)]""";

        // Act
        var result = RunGenerator(declaration);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        var generated = result.GeneratedSources.Single(s => s.HintName.Contains("OrderTouched.RaisesEvent.g.cs", StringComparison.Ordinal));
        var source = generated.SourceText.ToString();
        source.ShouldContain("namespace Probe.Entities");
        source.ShouldContain("public partial record OrderTouched");
        source.ShouldContain("Status");
        source.ShouldContain("DeliveryNote");
    }

    [Fact]
    public void HandAuthoredPartialRecordStub_WithTheSameNameAsAStringFormEvent_MergesWithoutBeingTreatedAsACollision()
    {
        // Arrange - a hand-authored `public partial record` stub is the developer's extension point
        // (identical to how [GenerateDto] payloads are extended), not a collision.
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Probe.Entities
            {
                public partial record OrderTouched
                {
                    public string ExtraNote => "hand-authored";
                }

                [RaisesEvent("OrderTouched", EventOperations.Created)]
                public sealed class Order
                {
                    public Guid Id { get; set; }
                    public string Status { get; set; } = string.Empty;
                }
            }
            """;

        // Act
        var result = RunGeneratorWithSource(source);

        // Assert - no collision diagnostic, and the generator still emits its partial for the merge
        result.Diagnostics.ShouldNotContain(d => d.Id == "DKRAISEVT004");
        result.GeneratedSources.ShouldContain(s => s.HintName.Contains("OrderTouched.RaisesEvent.g.cs", StringComparison.Ordinal));
    }

    private static GeneratorOutput RunGenerator(string declaration) =>
        RunGeneratorWithSource(EntitySource.Replace("{{Declaration}}", declaration));

    /// <summary>
    /// Runs <see cref="DKNet.EfCore.DtoGenerator.RaisesEventValidator"/> against a caller-supplied entity
    /// source (bypassing the single-<c>{{Declaration}}</c> template), for scenarios needing extra types
    /// or more than one entity in the compilation.
    /// </summary>
    private static GeneratorOutput RunGeneratorWithSource(string entitySource)
    {
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
            outputCompilation.GetDiagnostics().ToList(),
            runResult.Results.SelectMany(r => r.GeneratedSources).ToList());
    }

    /// <summary>
    /// Runs both <see cref="DKNet.EfCore.DtoGenerator.RaisesEventValidator"/> and the real
    /// <see cref="DKNet.EfCore.DtoGenerator.DtoGenerator"/> together, for the one scenario that needs to
    /// inspect actual payload-record content (the "no rule declared" baseline).
    /// </summary>
    private static GeneratorOutput RunBothGenerators(string declaration)
    {
        var entitySource = EntitySource.Replace("{{Declaration}}", declaration);

        // DtoGenerator deliberately omits "using System;" from generated payload source, relying on the
        // ImplicitUsings that every real consuming project has (Directory.Build.props, solution-wide) —
        // reproduce that same global using here rather than in every generated file.
        var compilation = CSharpCompilation.Create(
            "ProbeCompilation",
            [
                CSharpSyntaxTree.ParseText("global using System;"),
                CSharpSyntaxTree.ParseText(RuntimeContractSource),
                CSharpSyntaxTree.ParseText(GenerateDtoAttributeSource),
                CSharpSyntaxTree.ParseText(entitySource),
            ],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generators = new ISourceGenerator[]
        {
            new DKNet.EfCore.DtoGenerator.RaisesEventValidator().AsSourceGenerator(),
            new DKNet.EfCore.DtoGenerator.DtoGenerator().AsSourceGenerator(),
        };
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generators);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        var runResult = ((CSharpGeneratorDriver)driver).GetRunResult();

        return new GeneratorOutput(
            runResult.Results.SelectMany(r => r.Diagnostics).ToList(),
            outputCompilation.GetDiagnostics().ToList(),
            runResult.Results.SelectMany(r => r.GeneratedSources).ToList());
    }

    private sealed record GeneratorOutput(
        List<Diagnostic> Diagnostics,
        List<Diagnostic> CompilationDiagnostics,
        List<GeneratedSourceResult> GeneratedSources);

    #endregion
}
