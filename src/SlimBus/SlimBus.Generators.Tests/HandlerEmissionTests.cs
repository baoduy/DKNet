using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace SlimBus.Generators.Tests;

public class HandlerEmissionTests
{
    private const string DomainWithCreateAndUpdate = """
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
    public void Run_WithCrudCreate_EmitsHandlerCallingCtorAndAddAsync()
    {
        var (output, _, result) = GeneratorTestHelper.Run(DomainWithCreateAndUpdate, ApiWithProductDto);

        var text = GeneratedText(result);
        text.ShouldContain("class CreateProductHandler");
        text.ShouldContain("new global::MyDomain.Product(request.Name, request.Price)");
        text.ShouldContain("repository.AddAsync(entity, cancellationToken)");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    [Fact]
    public void Run_WithCrudUpdate_EmitsHandlerWithByIdSpecAndNotFoundError()
    {
        var (output, _, result) = GeneratorTestHelper.Run(DomainWithCreateAndUpdate, ApiWithProductDto);

        var text = GeneratedText(result);
        text.ShouldContain("class UpdatePriceProductHandler");
        text.ShouldContain("ProductByIdCrudSpec");
        text.ShouldContain("global::DKNet.SlimBus.Extensions.NotFoundError");
        text.ShouldContain("entity.UpdatePrice(request.Price)");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    [Fact]
    public void Run_WithHandWrittenHandlerPresent_SkipsGeneratedHandlerAndReportsInfo()
    {
        const string api = """
            using DKNet.EfCore.DtoGenerator;
            using DKNet.SlimBus.Extensions;
            using FluentResults;
            using MyDomain;
            using System.Threading;
            using System.Threading.Tasks;

            namespace MyApi
            {
                [GenerateDto(typeof(Product))]
                public partial record ProductDto;

                internal sealed class CustomCreateProductHandler : Fluents.Requests.IHandler<CreateProductRequest, ProductDto>
                {
                    public Task<IResult<ProductDto>> OnHandle(CreateProductRequest request, CancellationToken cancellationToken) =>
                        throw new System.NotImplementedException();
                }
            }
            """;

        var (_, diagnostics, result) = GeneratorTestHelper.Run(DomainWithCreateAndUpdate, api);

        var text = GeneratedText(result);
        text.ShouldNotContain("class CreateProductHandler");
        // Only the create request has a hand-written override; the update handler still generates.
        text.ShouldContain("class UpdatePriceProductHandler");
        diagnostics.ShouldContain(d => d.Id == "DKCRUDGEN005");
    }
}
