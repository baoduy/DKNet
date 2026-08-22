using System.Collections.Immutable;
using System.Globalization;
using System.IO;
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

                public RaisesEventAttribute(string label, EventOperations operations, params string[] properties)
                {
                    Label = label;
                    Operations = operations;
                    Properties = properties;
                }

                public RaisesEventAttribute(EventOperations operations, params string[] properties)
                {
                    Operations = operations;
                    Properties = properties;
                }

                public Type? EventType { get; }
                public string? Label { get; }
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
        // Arrange - LoyaltyMembershipEventsCreatedEvent is a plain (non-partial, non-record) type already
        // declared; composing to it must fail rather than silently reuse or collide with it. It does not
        // carry [GenerateDto], so the guidance offers only changing the label, never the type-naming form.
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Probe.Entities
            {
                public sealed class LoyaltyMembershipEventsCreatedEvent
                {
                    public int Points { get; set; }
                }

                [RaisesEvent("Events", EventOperations.Created)]
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
        error.GetMessage().ShouldContain("LoyaltyMembershipEventsCreatedEvent");
        error.GetMessage().ShouldContain("label");
        // DRK-676 §5 — colliding with an unrelated type must never suggest the type-naming remedy.
        error.GetMessage().ShouldNotContain("typeof");
    }

    [Fact]
    public void ComposedNameCollidingWithAGenerateDtoPayloadOfTheSameEntity_OffersBothRemedies()
    {
        // Arrange - CustomerCreatedEvent already exists as a hand-written [GenerateDto] payload of
        // Customer; the label-less rule below composes to the identical name. Since the colliding type
        // IS a payload of the SAME entity, the type-naming form is a viable remedy alongside the label.
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;
            using DKNet.EfCore.DtoGenerator;

            namespace Probe.Entities
            {
                [GenerateDto(typeof(Customer))]
                public partial record CustomerCreatedEvent;

                [RaisesEvent(EventOperations.Created)]
                public sealed class Customer
                {
                    public Guid Id { get; set; }
                }
            }
            """;

        // Act
        var result = RunGeneratorWithSource(source);

        // Assert
        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "DKRAISEVT004" && d.Severity == DiagnosticSeverity.Error);
        error.ShouldNotBeNull();
        error.GetMessage().ShouldContain("CustomerCreatedEvent");
        error.GetMessage().ShouldContain("typeof");
        error.GetMessage().ShouldContain("label");
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
        // Arrange - two DIFFERENT entities composing to the identical name: Order's label form
        // ("Touched") and OrderTouched's label-less form both compose "OrderTouchedCreatedEvent".
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Domain.Sales
            {
                [RaisesEvent("Touched", EventOperations.Created)]
                public sealed class Order
                {
                    public Guid Id { get; set; }
                }

                [RaisesEvent(EventOperations.Created)]
                public sealed class OrderTouched
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
        errors.ShouldAllBe(d => d.GetMessage().Contains("Order") && d.GetMessage().Contains("OrderTouched"));
        result.GeneratedSources.ShouldNotContain(s => s.HintName.Contains("OrderTouchedCreatedEvent", StringComparison.Ordinal));
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
        const string declaration = """[RaisesEvent(EventOperations.Created)]""";

        // Act
        var result = RunGenerator(declaration);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        var generated = result.GeneratedSources.Single(s => s.HintName.Contains("OrderCreatedEvent.RaisesEvent.g.cs", StringComparison.Ordinal));
        var source = generated.SourceText.ToString();
        source.ShouldContain("namespace Probe.Entities");
        source.ShouldContain("public partial record OrderCreatedEvent");
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
                public partial record OrderCreatedEvent
                {
                    public string ExtraNote => "hand-authored";
                }

                [RaisesEvent(EventOperations.Created)]
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
        result.GeneratedSources.ShouldContain(s => s.HintName.Contains("OrderCreatedEvent.RaisesEvent.g.cs", StringComparison.Ordinal));
    }

    // --- DRK-676 §5 — convention-based naming ---------------------------------------------------------

    [Fact]
    public void CreatedOperationWithNoLabel_ComposesEntityAndOperationOnly()
    {
        // Arrange
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Probe.Entities
            {
                [RaisesEvent(EventOperations.Created)]
                public sealed class Customer
                {
                    public Guid Id { get; set; }
                    public string Name { get; set; } = string.Empty;
                }
            }
            """;

        // Act
        var result = RunGeneratorWithSource(source);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        result.GeneratedSources.ShouldContain(s => s.HintName.Contains("CustomerCreatedEvent", StringComparison.Ordinal));
    }

    [Fact]
    public void UpdatedOperationLabelledTier_ComposesTheLabelBetweenEntityAndOperation()
    {
        // Arrange
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Probe.Entities
            {
                [RaisesEvent("Tier", EventOperations.Updated)]
                public sealed class Customer
                {
                    public Guid Id { get; set; }
                    public string Tier { get; set; } = string.Empty;
                }
            }
            """;

        // Act
        var result = RunGeneratorWithSource(source);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        result.GeneratedSources.ShouldContain(s => s.HintName.Contains("CustomerTierUpdatedEvent", StringComparison.Ordinal));
    }

    [Fact]
    public void UpdatedOperationNarrowedToStatus_ComposesThePropertyBetweenEntityAndOperation()
    {
        // Arrange
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Probe.Entities
            {
                [RaisesEvent(EventOperations.Updated, nameof(Customer.Status))]
                public sealed class Customer
                {
                    public Guid Id { get; set; }
                    public string Status { get; set; } = string.Empty;
                }
            }
            """;

        // Act
        var result = RunGeneratorWithSource(source);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        result.GeneratedSources.ShouldContain(s => s.HintName.Contains("CustomerStatusUpdatedEvent", StringComparison.Ordinal));
    }

    [Fact]
    public void LabelAndNarrowingPropertyTogether_ComposeLabelFirstThenProperty()
    {
        // Arrange
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Probe.Entities
            {
                [RaisesEvent("Send", EventOperations.Updated, nameof(Customer.Email))]
                public sealed class Customer
                {
                    public Guid Id { get; set; }
                    public string Email { get; set; } = string.Empty;
                }
            }
            """;

        // Act
        var result = RunGeneratorWithSource(source);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        result.GeneratedSources.ShouldContain(s => s.HintName.Contains("CustomerSendEmailUpdatedEvent", StringComparison.Ordinal));
    }

    [Fact]
    public void NarrowingPropertiesDeclaredInEitherOrder_ComposeTheIdenticalSortedName()
    {
        // Arrange - Customer narrows Status-then-Name, Order narrows Name-then-Status; both must sort
        // ordinally to the identical "NameStatus" segment regardless of declaration order.
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Probe.Entities
            {
                [RaisesEvent(EventOperations.Updated, nameof(Customer.Status), nameof(Customer.Name))]
                public sealed class Customer
                {
                    public Guid Id { get; set; }
                    public string Status { get; set; } = string.Empty;
                    public string Name { get; set; } = string.Empty;
                }

                [RaisesEvent(EventOperations.Updated, nameof(Order.Name), nameof(Order.Status))]
                public sealed class Order
                {
                    public Guid Id { get; set; }
                    public string Status { get; set; } = string.Empty;
                    public string Name { get; set; } = string.Empty;
                }
            }
            """;

        // Act
        var result = RunGeneratorWithSource(source);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        result.GeneratedSources.ShouldContain(s => s.HintName.Contains("CustomerNameStatusUpdatedEvent", StringComparison.Ordinal));
        result.GeneratedSources.ShouldContain(s => s.HintName.Contains("OrderNameStatusUpdatedEvent", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("sv-SE")]
    public void ComposedNameUnderNonInvariantCulture_MatchesTheInvariantBuild(string cultureName)
    {
        // Arrange - narrowed to "Iban" and "Id", the exact DRK-676 §5 culture scenario. Ordinal sorting
        // (not the current culture's) must decide the order, so tr-TR's dotless-i and sv-SE's collation
        // rules must never change the composed name.
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Probe.Entities
            {
                [RaisesEvent(EventOperations.Updated, nameof(Customer.Iban), nameof(Customer.Id))]
                public sealed class Customer
                {
                    public Guid Id { get; set; }
                    public string Iban { get; set; } = string.Empty;
                }
            }
            """;

        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        GeneratorOutput result;

        // Act
        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            result = RunGeneratorWithSource(source);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        result.GeneratedSources.ShouldContain(s => s.HintName.Contains("CustomerIbanIdUpdatedEvent", StringComparison.Ordinal));
    }

    [Fact]
    public void NarrowingPropertyDeclaredTwice_AppearsOnceInTheComposedName()
    {
        // Arrange
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Probe.Entities
            {
                [RaisesEvent(EventOperations.Updated, nameof(Customer.Status), nameof(Customer.Status))]
                public sealed class Customer
                {
                    public Guid Id { get; set; }
                    public string Status { get; set; } = string.Empty;
                }
            }
            """;

        // Act
        var result = RunGeneratorWithSource(source);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        result.GeneratedSources.ShouldContain(s => s.HintName.Contains("CustomerStatusUpdatedEvent", StringComparison.Ordinal));
        result.GeneratedSources.Count(s => s.HintName.Contains("RaisesEvent.g.cs", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public void OperationsDeclaredInAnyCombination_ComposeInCanonicalCreatedUpdatedDeletedOrder()
    {
        // Arrange - flags combined out of canonical order (Deleted | Created | Updated)
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Probe.Entities
            {
                [RaisesEvent(EventOperations.Deleted | EventOperations.Created | EventOperations.Updated)]
                public sealed class Customer
                {
                    public Guid Id { get; set; }
                }
            }
            """;

        // Act
        var result = RunGeneratorWithSource(source);

        // Assert - exactly ONE record covering all three operations, named in canonical order
        result.Diagnostics.ShouldBeEmpty();
        var generated = result.GeneratedSources.Single(s => s.HintName.Contains("RaisesEvent.g.cs", StringComparison.Ordinal));
        generated.HintName.ShouldContain("CustomerCreatedUpdatedDeletedEvent");
    }

    [Fact]
    public void RenamingTheLabelOnAConventionDeclaration_ChangesOnlyTheName_NotThePayloadShape()
    {
        // Arrange - identical entity/properties/narrowing, only the label differs (simulates the
        // upgrade rename: a pre-upgrade literal name is now a label on the same declaration).
        const string beforeSource = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Probe.Entities
            {
                [RaisesEvent("Old", EventOperations.Updated, nameof(Customer.Tier))]
                public sealed class Customer
                {
                    public Guid Id { get; set; }
                    public string Tier { get; set; } = string.Empty;
                    public string Name { get; set; } = string.Empty;
                }
            }
            """;
        const string afterSource = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Probe.Entities
            {
                [RaisesEvent("New", EventOperations.Updated, nameof(Customer.Tier))]
                public sealed class Customer
                {
                    public Guid Id { get; set; }
                    public string Tier { get; set; } = string.Empty;
                    public string Name { get; set; } = string.Empty;
                }
            }
            """;

        // Act
        var before = RunGeneratorWithSource(beforeSource);
        var after = RunGeneratorWithSource(afterSource);

        // Assert - the names differ (the rename took effect) but the payload member set is identical
        var beforeText = before.GeneratedSources
            .Single(s => s.HintName.Contains("RaisesEvent.g.cs", StringComparison.Ordinal)).SourceText.ToString();
        var afterText = after.GeneratedSources
            .Single(s => s.HintName.Contains("RaisesEvent.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        beforeText.ShouldContain("CustomerOldTierUpdatedEvent");
        afterText.ShouldContain("CustomerNewTierUpdatedEvent");
        foreach (var member in new[] { "Id", "Tier", "Name" })
        {
            beforeText.ShouldContain(member);
            afterText.ShouldContain(member);
        }
    }

    [Fact]
    public void TwoDeclarationsOnOneEntity_ComposingTheSameName_FailTheBuildIdentifyingBoth()
    {
        // Arrange - DKRAISEVT008: same entity, narrowing declared in opposite order, so both compose
        // to "CustomerNameStatusUpdatedEvent" — no longer the silent merge of the pre-upgrade behaviour.
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Probe.Entities
            {
                [RaisesEvent(EventOperations.Updated, nameof(Customer.Status), nameof(Customer.Name))]
                [RaisesEvent(EventOperations.Updated, nameof(Customer.Name), nameof(Customer.Status))]
                public sealed class Customer
                {
                    public Guid Id { get; set; }
                    public string Status { get; set; } = string.Empty;
                    public string Name { get; set; } = string.Empty;
                }
            }
            """;

        // Act
        var result = RunGeneratorWithSource(source);

        // Assert
        var errors = result.Diagnostics.Where(d => d.Id == "DKRAISEVT008" && d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Count.ShouldBe(2);
        errors.ShouldAllBe(d => d.GetMessage().Contains("Customer") && d.GetMessage().Contains("CustomerNameStatusUpdatedEvent"));
        result.GeneratedSources.ShouldNotContain(s => s.HintName.Contains("CustomerNameStatusUpdatedEvent", StringComparison.Ordinal));
    }

    [Fact]
    public void LabelThatCannotComposeIntoALegalIdentifier_FailsTheBuildAndGeneratesNoRecord()
    {
        // Arrange - "Tier Level" cannot form part of a valid C# identifier once composed
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Probe.Entities
            {
                [RaisesEvent("Tier Level", EventOperations.Created)]
                public sealed class Customer
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
        error.GetMessage().ShouldContain("Customer");
        error.GetMessage().ShouldContain("event record name");
        result.GeneratedSources.ShouldNotContain(s => s.HintName.Contains("RaisesEvent.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void DeclarationNamingNoOperation_FailsTheBuildStatingAtLeastOneOperationRequired()
    {
        // Arrange - DKRAISEVT007: the convention form with an empty operations bitmask
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Probe.Entities
            {
                [RaisesEvent((EventOperations)0)]
                public sealed class Customer
                {
                    public Guid Id { get; set; }
                }
            }
            """;

        // Act
        var result = RunGeneratorWithSource(source);

        // Assert
        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "DKRAISEVT007" && d.Severity == DiagnosticSeverity.Error);
        error.ShouldNotBeNull();
        error.GetMessage().ShouldContain("Customer");
        error.GetMessage().ShouldContain("at least one operation");
        result.GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void DeclarationNamingIntConstantZeroOperations_FailsTheBuildStatingAtLeastOneOperationRequired()
    {
        // Arrange - DKRAISEVT007: an int constant (not an EventOperations-typed expression) implicitly
        // converts to the flags-enum parameter and must route into the same convention-form validation as
        // `(EventOperations)0`, rather than being silently ignored because its static type is Int32.
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Probe.Entities
            {
                [RaisesEvent(0)]
                public sealed class Customer
                {
                    public Guid Id { get; set; }
                }
            }
            """;

        // Act
        var result = RunGeneratorWithSource(source);

        // Assert
        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "DKRAISEVT007" && d.Severity == DiagnosticSeverity.Error);
        error.ShouldNotBeNull();
        error.GetMessage().ShouldContain("Customer");
        error.GetMessage().ShouldContain("at least one operation");
        result.GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void DeclarationNamingIntConstantZeroWithNarrowingProperty_FailsTheBuildRatherThanCrashingTheGenerator()
    {
        // Arrange - DKRAISEVT007: an int constant `0` alongside a narrowing property argument must still
        // route into the convention form and report the missing-operation error, rather than falling into
        // the label-form branch where the narrowing property name would be treated as an attempted
        // Convert.ToInt32 operations value and crash the generator (surfacing as CS8785).
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Probe.Entities
            {
                [RaisesEvent(0, nameof(Customer.Status))]
                public sealed class Customer
                {
                    public Guid Id { get; set; }
                    public string Status { get; set; } = string.Empty;
                }
            }
            """;

        // Act
        var result = RunGeneratorWithSource(source);

        // Assert
        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "DKRAISEVT007" && d.Severity == DiagnosticSeverity.Error);
        error.ShouldNotBeNull();
        error.GetMessage().ShouldContain("Customer");
        result.CompilationDiagnostics.ShouldNotContain(d => d.Id == "CS8785");
        result.GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void TypeNamingDeclarationNamingNoOperation_AlsoFailsTheBuild()
    {
        // Arrange - DKRAISEVT007 applies to ANY declaration form, type-naming included
        const string declaration = """[RaisesEvent(typeof(OrderPlacedEvent), (EventOperations)0)]""";

        // Act
        var result = RunGenerator(declaration);

        // Assert
        var error = result.Diagnostics.FirstOrDefault(d => d.Id == "DKRAISEVT007" && d.Severity == DiagnosticSeverity.Error);
        error.ShouldNotBeNull();
        result.Diagnostics.ShouldNotContain(d => d.Id == "DKRAISEVT002");
        result.GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void NarrowingWithoutUpdatedOperation_StillComposesThePropertyIntoTheNameAndWarns()
    {
        // Arrange - R9: narrowing on a declaration without Updated still warns (DKRAISEVT003) AND
        // still contributes to the composed name, so the name never misrepresents what was declared.
        const string source = """
            using System;
            using DKNet.EfCore.Abstractions.Events;

            namespace Probe.Entities
            {
                [RaisesEvent(EventOperations.Deleted, nameof(Customer.Status))]
                public sealed class Customer
                {
                    public Guid Id { get; set; }
                    public string Status { get; set; } = string.Empty;
                }
            }
            """;

        // Act
        var result = RunGeneratorWithSource(source);

        // Assert
        result.Diagnostics.ShouldNotContain(d => d.Severity == DiagnosticSeverity.Error);
        result.Diagnostics.ShouldContain(d => d.Id == "DKRAISEVT003" && d.Severity == DiagnosticSeverity.Warning);
        result.GeneratedSources.ShouldContain(s => s.HintName.Contains("CustomerStatusDeletedEvent", StringComparison.Ordinal));
    }

    [Fact]
    public void EventNameComposer_ExistsInOnePhysicalFile_LinkedIntoTheGeneratorProject_NotDuplicated()
    {
        // Arrange - locate the repo's src/ directory from the test assembly's own on-disk output path.
        var srcDir = FindSrcDirectory();
        var abstractionsComposerPath = Path.Combine(
            srcDir, "EfCore", "DKNet.EfCore.Abstractions", "Events", "EventNameComposer.cs");
        var generatorProjectDir = Path.Combine(srcDir, "EfCore", "DKNet.EfCore.DtoGenerator");
        var generatorCsprojPath = Path.Combine(generatorProjectDir, "DKNet.EfCore.DtoGenerator.csproj");

        // Act
        var composerExists = File.Exists(abstractionsComposerPath);
        var csprojContent = File.ReadAllText(generatorCsprojPath);
        var duplicateImplementations = Directory
            .EnumerateFiles(generatorProjectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                        Path.GetFileName(f) != "EventNameComposer.cs")
            .Select(File.ReadAllText)
            .Where(text => text.Contains("static string Compose(", StringComparison.Ordinal))
            .ToList();

        // Assert - the generator links the SAME file rather than declaring its own copy or a ProjectReference
        composerExists.ShouldBeTrue();
        csprojContent.ShouldContain(
            """<Compile Include="..\DKNet.EfCore.Abstractions\Events\EventNameComposer.cs" Link="Shared\EventNameComposer.cs"/>""");
        csprojContent.ShouldNotContain("ProjectReference Include=\"..\\DKNet.EfCore.Abstractions");
        duplicateImplementations.ShouldBeEmpty();
    }

    private static string FindSrcDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.Name != "src")
            dir = dir.Parent;

        dir.ShouldNotBeNull();
        return dir!.FullName;
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
