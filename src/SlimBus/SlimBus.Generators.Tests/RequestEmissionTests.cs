using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace SlimBus.Generators.Tests;

public class RequestEmissionTests
{
    private const string DomainWithCreateCtor = """
        using System;
        using DKNet.EfCore.Abstractions.Attributes;
        using DKNet.EfCore.Abstractions.Entities;

        namespace MyDomain
        {
            public class Product : IEntity<Guid>
            {
                [CrudCreate]
                public Product(string name, decimal price)
                {
                    Name = name;
                    Price = price;
                }

                [CrudUpdate]
                public void UpdatePrice(decimal price) => Price = price;

                public Guid Id { get; private set; }
                public string Name { get; private set; } = string.Empty;
                public decimal Price { get; private set; }
            }
        }
        """;

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
    public void Run_WithCrudCreateCtor_EmitsCreateRequestImplementingIWitResponseOfDto()
    {
        var (output, _, result) = GeneratorTestHelper.Run(DomainWithCreateCtor, ApiWithProductDto);

        var text = GeneratedText(result);
        text.ShouldContain("sealed partial record CreateProductRequest");
        text.ShouldContain("IWitResponse<global::MyApi.ProductDto>");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    [Fact]
    public void Run_WithCrudUpdateMethod_EmitsRequestWithRouteBoundId()
    {
        var (output, _, result) = GeneratorTestHelper.Run(DomainWithCreateCtor, ApiWithProductDto);

        var text = GeneratedText(result);
        text.ShouldContain("IWithKey<global::System.Guid>");
        text.ShouldContain("public global::System.Guid Id");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    [Fact]
    public void Run_WithRequiredAnnotationOnParameter_CopiesAnnotationToProperty()
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
                    public Product([Required] string name, decimal price)
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
        text.ShouldContain("[global::System.ComponentModel.DataAnnotations.Required]");
        text.ShouldContain("public required string Name");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    [Fact]
    public void Run_WithNameOverride_UsesOverriddenRequestName()
    {
        const string domain = """
            using System;
            using DKNet.EfCore.Abstractions.Attributes;
            using DKNet.EfCore.Abstractions.Entities;

            namespace MyDomain
            {
                public class Product : IEntity<Guid>
                {
                    [CrudCreate(Name = "MakeProductRequest")]
                    public Product(string name, decimal price)
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
        text.ShouldContain("sealed partial record MakeProductRequest");
        text.ShouldNotContain("CreateProductRequest");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }
}
