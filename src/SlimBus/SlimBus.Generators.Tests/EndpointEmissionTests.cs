using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace SlimBus.Generators.Tests;

public class EndpointEmissionTests
{
    private const string DomainWithCreateAndTwoUpdates = """
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

                [CrudUpdate]
                public void UpdateName(string name) => Name = name;

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
    public void Run_WithFullSlice_EmitsMapCrudExtensionComposingExistingMappers()
    {
        var (output, _, result) = GeneratorTestHelper.Run(DomainWithCreateAndTwoUpdates, ApiWithProductDto);

        var text = GeneratedText(result);
        text.ShouldContain("public static class ProductCrudEndpointExtensions");
        text.ShouldContain("MapProductCrud(");
        text.ShouldContain("MapGetById<");
        text.ShouldContain("MapGetList<");
        text.ShouldContain("MapDeleteById<");
        text.ShouldContain("MapPost<CreateProductRequest");
        text.ShouldContain("MapPutById<UpdatePriceProductRequest");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    [Fact]
    public void Run_WithTwoUpdates_MapsSecondUpdateToKebabRoute()
    {
        var (output, _, result) = GeneratorTestHelper.Run(DomainWithCreateAndTwoUpdates, ApiWithProductDto);

        var text = GeneratedText(result);
        text.ShouldContain("group.MapPutById<UpdatePriceProductRequest, global::System.Guid, global::MyApi.ProductDto>(\"{id}\");");
        text.ShouldContain("group.MapPutById<UpdateNameProductRequest, global::System.Guid, global::MyApi.ProductDto>(\"{id}/update-name\");");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    [Fact]
    public void Run_WithoutAspCoreReference_SkipsEndpointFile()
    {
        var (_, _, result) = GeneratorTestHelper.Run(
            DomainWithCreateAndTwoUpdates, ApiWithProductDto, ["DKNet.AspCore.Extensions"]);

        result.Results.SelectMany(r => r.GeneratedSources)
            .ShouldNotContain(s => s.HintName.Contains("CrudEndpoints"));
    }
}
