using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace SlimBus.Generators.Tests;

/// <summary>
/// Generated request properties must carry the entity parameter's DataAnnotations verbatim — named
/// arguments, numeric/bool/char literals, arrays, and flags-enum values — so validation on the generated
/// request behaves identically to a hand-written one.
/// </summary>
public class AnnotationFormattingTests
{
    private const string ApiWithProductDto = """
        using DKNet.EfCore.DtoGenerator;
        using MyDomain;

        namespace MyApi
        {
            [GenerateDto(typeof(Product))]
            public partial record ProductDto;
        }
        """;

    private static string GeneratedText(GeneratorDriverRunResult result) =>
        string.Join("\n", result.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));

    [Fact]
    public void Run_WithNamedAndDoubleAnnotationArguments_RendersThemVerbatim()
    {
        const string domain = """
            using System;
            using System.ComponentModel.DataAnnotations;
            using DKNet.EfCore.Abstractions.Attributes;
            using DKNet.EfCore.Abstractions.Entities;

            namespace MyDomain
            {
                public class Product : IEntity<Guid>
                {
                    [CrudCreate]
                    public Product([StringLength(50, MinimumLength = 2)] string name, [Range(0.5, 9.9)] decimal price)
                    {
                        Name = name;
                        Price = price;
                    }

                    public Guid Id { get; private set; }
                    public string Name { get; private set; } = string.Empty;
                    public decimal Price { get; private set; }
                }
            }
            """;

        var (output, _, result) = GeneratorTestHelper.Run(domain, ApiWithProductDto);

        var text = GeneratedText(result);
        text.ShouldContain("StringLength(50, MinimumLength = 2)");
        text.ShouldContain("Range(0.5, 9.9)");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    [Fact]
    public void Run_WithBoolCharArrayAndFlagsEnumAnnotationArguments_RendersLiteralsAndCastsCombinedFlags()
    {
        // The attribute lives in the DataAnnotations namespace so the generator's namespace filter picks
        // it up; its arguments exercise every literal kind the formatter must round-trip.
        const string domain = """
            using System;
            using DKNet.EfCore.Abstractions.Attributes;
            using DKNet.EfCore.Abstractions.Entities;

            namespace System.ComponentModel.DataAnnotations
            {
                [Flags]
                public enum TagKinds { None = 0, Alpha = 1, Beta = 2 }

                public sealed class TaggedAttribute : Attribute
                {
                    public TaggedAttribute(char code, bool strict, TagKinds kinds, string[] tags) { }
                }
            }

            namespace MyDomain
            {
                using System.ComponentModel.DataAnnotations;

                public class Product : IEntity<Guid>
                {
                    [CrudCreate]
                    public Product([Tagged('x', true, TagKinds.Alpha | TagKinds.Beta, new[] { "one", "two" })] string name)
                    {
                        Name = name;
                    }

                    public Guid Id { get; private set; }
                    public string Name { get; private set; } = string.Empty;
                }
            }
            """;

        var (output, _, result) = GeneratorTestHelper.Run(domain, ApiWithProductDto);

        var text = GeneratedText(result);
        text.ShouldContain("'x', true");
        text.ShouldContain("new[] { \"one\", \"two\" }");
        // Alpha | Beta (= 3) matches no single named member, so the formatter falls back to a cast literal.
        text.ShouldContain("(global::System.ComponentModel.DataAnnotations.TagKinds)3");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }
}
