using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;

namespace EfCore.DtoGenerator.Tests;

/// <summary>
///     DRK-437 acceptance criteria — <c>@unit</c> scenarios for the <c>[GenerateEvent]</c> source
///     generator, driven in-process against an in-memory compilation exactly like the existing
///     <c>DtoGeneratorPrecedenceTests</c>. Assertions are on generator diagnostics and generated source
///     text; no real compilation of the output is required.
/// </summary>
public class EventGeneratorDiagnosticsTests
{
    #region Constants

    private const string EventAttributeSource = """
        using System;

        namespace DKNet.EfCore.DtoGenerator
        {
            [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
            internal sealed class GenerateEventAttribute : Attribute
            {
                public string? NameSuffix { get; set; }
                public EventKinds Kinds { get; set; }
                public string[] Properties { get; set; } = [];
                public string[] Include { get; set; } = [];
                public string[] Exclude { get; set; } = [];
                public bool IgnoreComplexType { get; set; }
            }

            [Flags]
            internal enum EventKinds
            {
                Created = 1,
                Updated = 2,
                Deleted = 4,
            }
        }
        """;

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

            [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
            public sealed class GeneratedEventAttribute : Attribute
            {
                public GeneratedEventAttribute(Type entityType, Type eventType, EventOperations operations,
                    params string[] properties)
                {
                    EntityType = entityType;
                    EventType = eventType;
                    Operations = operations;
                    Properties = properties;
                }

                public Type EntityType { get; }
                public Type EventType { get; }
                public EventOperations Operations { get; }
                public string[] Properties { get; }
            }
        }
        """;

    private const string EntitySource = """
        using System;

        namespace Probe.Entities
        {
            public sealed class DeliveryAddress
            {
                public string City { get; set; } = string.Empty;
            }
        }
        """;

    private static readonly MetadataReference[] References =
    [
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute).Assembly.Location),
    ];

    #endregion

    #region Methods

    [Fact]
    public void Narrowing_UnknownProperty_FailsTheBuild()
    {
        // Arrange
        const string declaration = """
            [GenerateEvent(Kinds = EventKinds.Updated, NameSuffix = "StatusChanged", Properties = new[] { "Statuz" })]
            """;

        // Act
        var diagnostics = RunGenerator(declaration).Diagnostics;

        // Assert
        var error = diagnostics.FirstOrDefault(d => d.Id == "DKDTOEVT001" && d.Severity == DiagnosticSeverity.Error);
        error.ShouldNotBeNull();
        error.GetMessage().ShouldContain("Statuz");
    }

    [Fact]
    public void Narrowing_NestedPath_FailsTheBuild()
    {
        // Arrange
        const string declaration = """
            [GenerateEvent(Kinds = EventKinds.Updated, NameSuffix = "StatusChanged", Properties = new[] { "DeliveryAddress.City" })]
            """;

        // Act
        var diagnostics = RunGenerator(declaration).Diagnostics;

        // Assert
        var error = diagnostics.FirstOrDefault(d => d.Id == "DKDTOEVT001" && d.Severity == DiagnosticSeverity.Error);
        error.ShouldNotBeNull();
        error.GetMessage().ShouldContain("nested path");
    }

    [Fact]
    public void TwoDeclarations_ResolvingToSameName_FailTheBuild()
    {
        // Arrange - two no-suffix create declarations both resolve to OrderCreatedEvent
        const string declaration = """
            [GenerateEvent(Kinds = EventKinds.Created)]
            [GenerateEvent(Kinds = EventKinds.Created)]
            """;

        // Act
        var diagnostics = RunGenerator(declaration).Diagnostics;

        // Assert
        var error = diagnostics.FirstOrDefault(d => d.Id == "DKDTOEVT002" && d.Severity == DiagnosticSeverity.Error);
        error.ShouldNotBeNull();
        error.GetMessage().ShouldContain("OrderCreatedEvent");
    }

    [Fact]
    public void Narrowing_OnCreateOnlyDeclaration_WarnsAndBuilds()
    {
        // Arrange - narrowing on a create-only declaration is meaningless
        const string declaration = """
            [GenerateEvent(Kinds = EventKinds.Created, Properties = new[] { "Status" })]
            """;

        // Act
        var result = RunGenerator(declaration);

        // Assert
        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var warning = result.Diagnostics.FirstOrDefault(d => d.Id == "DKDTOEVT004" && d.Severity == DiagnosticSeverity.Warning);
        warning.ShouldNotBeNull();
        result.GeneratedSources.ShouldNotBeEmpty();
    }

    [Fact]
    public void Declaration_CoveringMultipleKindsWithoutSuffix_FailsTheBuild()
    {
        // Arrange - no-suffix multi-kind naming is ambiguous
        const string declaration = """
            [GenerateEvent(Kinds = EventKinds.Created | EventKinds.Updated)]
            """;

        // Act
        var diagnostics = RunGenerator(declaration).Diagnostics;

        // Assert
        var error = diagnostics.FirstOrDefault(d => d.Id == "DKDTOEVT003" && d.Severity == DiagnosticSeverity.Error);
        error.ShouldNotBeNull();
        error.GetMessage().ShouldContain("NameSuffix");
    }

    [Fact]
    public void GeneratedRecord_NamedFromEntityAndOperation_WhenNoSuffix()
    {
        // Arrange
        const string declaration = """
            [GenerateEvent(Kinds = EventKinds.Created)]
            """;

        // Act
        var result = RunGenerator(declaration);

        // Assert
        result.GeneratedSources.ShouldContain(s => s.Contains("public sealed record OrderCreatedEvent", StringComparison.Ordinal));
    }

    [Fact]
    public void DistinctDeclarations_ProduceDistinctlyNamedRecords()
    {
        // Arrange
        const string declaration = """
            [GenerateEvent(Kinds = EventKinds.Created)]
            [GenerateEvent(Kinds = EventKinds.Updated)]
            """;

        // Act
        var result = RunGenerator(declaration);

        // Assert
        result.GeneratedSources.ShouldContain(s => s.Contains("public sealed record OrderCreatedEvent", StringComparison.Ordinal));
        result.GeneratedSources.ShouldContain(s => s.Contains("public sealed record OrderUpdatedEvent", StringComparison.Ordinal));
    }

    [Fact]
    public void RegistrationAttribute_IsEmitted_WhenRuntimeContractIsResolvable()
    {
        // Arrange
        const string declaration = """
            [GenerateEvent(Kinds = EventKinds.Created)]
            """;

        // Act
        var result = RunGenerator(declaration, includeRuntimeContract: true);

        // Assert
        var registration = result.GeneratedSources
            .Select(s => s.Split('\n').FirstOrDefault(l => l.Contains("[assembly:", StringComparison.Ordinal)))
            .FirstOrDefault(l => l is not null);
        registration.ShouldNotBeNull();
        registration.ShouldContain(
            "GeneratedEventAttribute(typeof(global::Probe.Entities.Order), typeof(global::Probe.Entities.OrderCreatedEvent), DKNet.EfCore.Abstractions.Events.EventOperations.Created)");
    }

    [Fact]
    public void GeneratedSource_BuildsUsingsBeforeAssemblyAttribute()
    {
        // Arrange - a record referencing non-special types needs using directives, and they must
        // precede the assembly-level registration for the emitted source to be valid C# (CS1529).
        const string declaration = """
            [GenerateEvent(Kinds = EventKinds.Created)]
            """;

        // Act
        var result = RunGenerator(declaration, includeRuntimeContract: true);

        // Assert
        var source = result.GeneratedSources.Single(s => s.Contains("OrderCreatedEvent", StringComparison.Ordinal));
        var usingIndex = source.IndexOf("using ", StringComparison.Ordinal);
        var assemblyIndex = source.IndexOf("[assembly:", StringComparison.Ordinal);
        usingIndex.ShouldNotBe(-1);
        assemblyIndex.ShouldNotBe(-1);
        usingIndex.ShouldBeLessThan(assemblyIndex);
    }

    private static GeneratorOutput RunGenerator(string declaration, bool includeRuntimeContract = false)
    {
        var entitySource = $$"""
            using System;
            using System.ComponentModel.DataAnnotations;
            using DKNet.EfCore.DtoGenerator;

            namespace Probe.Entities
            {
                {{declaration}}
                public sealed class Order
                {
                    public Guid Id { get; set; }
                    [Required]
                    public string Status { get; set; } = string.Empty;
                    public string DeliveryNote { get; set; } = string.Empty;
                    public DeliveryAddress? DeliveryAddress { get; set; }
                }
            }
            """;

        var trees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(EventAttributeSource),
            CSharpSyntaxTree.ParseText(EntitySource),
            CSharpSyntaxTree.ParseText(entitySource),
        };
        if (includeRuntimeContract)
            trees.Add(CSharpSyntaxTree.ParseText(RuntimeContractSource));

        var compilation = CSharpCompilation.Create(
            "ProbeCompilation",
            trees,
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DKNet.EfCore.DtoGenerator.EventGenerator().AsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create([generator], optionsProvider: new EmptyAnalyzerConfigOptionsProvider());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var runResult = ((CSharpGeneratorDriver)driver).GetRunResult();

        return new GeneratorOutput(
            runResult.Results.SelectMany(r => r.Diagnostics).ToList(),
            runResult.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()).ToList());
    }

    private sealed record GeneratorOutput(List<Diagnostic> Diagnostics, List<string> GeneratedSources);

    #endregion

    #region Helpers

    private sealed class EmptyAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new EmptyAnalyzerConfigOptions();

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;

        private sealed class EmptyAnalyzerConfigOptions : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, out string value)
            {
                value = string.Empty;
                return false;
            }
        }
    }

    #endregion
}