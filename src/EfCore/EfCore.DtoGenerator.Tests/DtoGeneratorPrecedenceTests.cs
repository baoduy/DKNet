using System.Collections.Immutable;
using DKNet.EfCore.DtoGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;

namespace EfCore.DtoGenerator.Tests;

/// <summary>
///     In-process generator-driver tests for the IgnoreComplexType precedence rules
///     (per-DTO value > project-wide MSBuild property > built-in default of true),
///     driving <see cref="DtoGenerator"/> directly against an in-memory compilation.
/// </summary>
public class DtoGeneratorPrecedenceTests
{
    #region Constants

    private const string AttributeSource = """
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
        using System.Collections.Generic;

        namespace Probe.Entities
        {
            public sealed class Address
            {
                public string City { get; set; } = string.Empty;
            }

            public sealed class Order
            {
                public int OrderId { get; set; }
            }

            public sealed class Customer
            {
                public int CustomerId { get; set; }
                public string Name { get; set; } = string.Empty;
                public string Email { get; set; } = string.Empty;
                public Address? PrimaryAddress { get; set; }
                public ICollection<Order>? Orders { get; set; }
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
    public void Default_NoFlag_ExcludesNavigationProperties()
    {
        // Arrange
        var dtoSource = BuildDtoSource("public partial record CustomerDto;");

        // Act
        var source = RunGenerator(dtoSource, projectWideIgnoreComplexType: null);

        // Assert
        ContainsProperties(source, "CustomerId", "Name", "Email").ShouldBeTrue();
        ContainsProperties(source, "PrimaryAddress", "Orders").ShouldBeFalse();
    }

    [Fact]
    public void PerDtoFalse_OverridesBuiltInDefault_IncludesNavigationProperties()
    {
        // Arrange
        var dtoSource = BuildDtoSource("public partial record CustomerDto;", "IgnoreComplexType = false");

        // Act
        var source = RunGenerator(dtoSource, projectWideIgnoreComplexType: null);

        // Assert
        ContainsProperties(source, "CustomerId", "PrimaryAddress", "Orders").ShouldBeTrue();
    }

    [Fact]
    public void ProjectWideFalse_WithNoFlag_IncludesNavigationProperties()
    {
        // Arrange
        var dtoSource = BuildDtoSource("public partial record CustomerDto;");

        // Act
        var source = RunGenerator(dtoSource, projectWideIgnoreComplexType: "false");

        // Assert
        ContainsProperties(source, "CustomerId", "PrimaryAddress", "Orders").ShouldBeTrue();
    }

    [Fact]
    public void PerDtoTrue_OverridesProjectWideFalse_ExcludesNavigationProperties()
    {
        // Arrange
        var dtoSource = BuildDtoSource("public partial record CustomerDto;", "IgnoreComplexType = true");

        // Act
        var source = RunGenerator(dtoSource, projectWideIgnoreComplexType: "false");

        // Assert
        ContainsProperties(source, "CustomerId", "Name", "Email").ShouldBeTrue();
        ContainsProperties(source, "PrimaryAddress", "Orders").ShouldBeFalse();
    }

    [Fact]
    public void ProjectWideUnparseable_FallsBackToBuiltInDefault_ExcludesNavigationProperties()
    {
        // Arrange
        var dtoSource = BuildDtoSource("public partial record CustomerDto;");

        // Act
        var source = RunGenerator(dtoSource, projectWideIgnoreComplexType: "not-a-bool");

        // Assert
        ContainsProperties(source, "CustomerId", "Name", "Email").ShouldBeTrue();
        ContainsProperties(source, "PrimaryAddress", "Orders").ShouldBeFalse();
    }

    [Fact]
    public void ProjectWideTrue_WithNoFlag_ExcludesNavigationProperties()
    {
        // Arrange
        var dtoSource = BuildDtoSource("public partial record CustomerDto;");

        // Act
        var source = RunGenerator(dtoSource, projectWideIgnoreComplexType: "true");

        // Assert
        ContainsProperties(source, "CustomerId", "Name", "Email").ShouldBeTrue();
        ContainsProperties(source, "PrimaryAddress", "Orders").ShouldBeFalse();
    }

    #endregion

    #region Internals

    private static string BuildDtoSource(string dtoDeclaration, string? ignoreComplexTypeArg = null)
    {
        var arg = ignoreComplexTypeArg is null ? string.Empty : $", {ignoreComplexTypeArg}";
        return $$"""
            using DKNet.EfCore.DtoGenerator;

            namespace Probe.Dtos
            {
                [GenerateDto(typeof(Probe.Entities.Customer){{arg}})]
                {{dtoDeclaration}}
            }
            """;
    }

    private static string RunGenerator(string dtoSource, string? projectWideIgnoreComplexType)
    {
        var compilation = CSharpCompilation.Create(
            "ProbeCompilation",
            [
                CSharpSyntaxTree.ParseText(AttributeSource),
                CSharpSyntaxTree.ParseText(EntitySource),
                CSharpSyntaxTree.ParseText(dtoSource),
            ],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var optionsProvider = new TestAnalyzerConfigOptionsProvider(projectWideIgnoreComplexType);
        var generator = new DtoGenerator().AsSourceGenerator();
        var driver = CSharpGeneratorDriver.Create([generator], optionsProvider: optionsProvider);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var runResult = driver.GetRunResult();
        var generatedSources = runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString())
            .ToList();

        generatedSources.ShouldNotBeEmpty("generator should have produced DTO source");
        return generatedSources.Count == 1 ? generatedSources[0] : string.Join('\n', generatedSources);
    }

    private static bool ContainsProperties(string source, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            // Generated DTOs expose properties like "public int CustomerId { ... }" or "public Address PrimaryAddress { ... }"
            if (!source.Contains($"{propertyName} ", StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly TestAnalyzerConfigOptions _globalOptions;

        public TestAnalyzerConfigOptionsProvider(string? projectWideIgnoreComplexType)
        {
            _globalOptions = new TestAnalyzerConfigOptions(projectWideIgnoreComplexType);
        }

        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _globalOptions;
    }

    private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly string? _projectWideIgnoreComplexType;

        public TestAnalyzerConfigOptions(string? projectWideIgnoreComplexType)
        {
            _projectWideIgnoreComplexType = projectWideIgnoreComplexType;
        }

        public override bool TryGetValue(string key, out string value)
        {
            value = string.Empty;
            if (key == "build_property.DtoGeneratorIgnoreComplexType" && _projectWideIgnoreComplexType is not null)
            {
                value = _projectWideIgnoreComplexType;
                return true;
            }

            return false;
        }
    }

    #endregion
}