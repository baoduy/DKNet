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

    private const string DomainWithCreateAndAction = """
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

                [CrudAction("archive")]
                public void Discontinue() => Name = "(discontinued) " + Name;

                public Guid Id { get; private set; }
                public string Name { get; private set; } = string.Empty;
                public decimal Price { get; private set; }
            }
        }
        """;

    [Fact]
    public void Run_WithCrudAction_EmitsHandlerWithByIdSpecAndNotFoundErrorAndUsingLine()
    {
        // An action on an entity with no update members must still emit the ByIdCrudSpec and the
        // DKNet.EfCore.Specifications.Extensions using line (spec Rules, edge cases): both are gated on
        // "updatesToEmit.Length > 0 || actionsToEmit.Length > 0", not on updates alone.
        var (output, _, result) = GeneratorTestHelper.Run(DomainWithCreateAndAction, ApiWithProductDto);

        var text = GeneratedText(result);
        text.ShouldContain("using DKNet.EfCore.Specifications.Extensions;");
        text.ShouldContain("class ProductByIdCrudSpec");
        text.ShouldContain("class DiscontinueProductHandler");
        text.ShouldContain("global::DKNet.SlimBus.Extensions.NotFoundError");
        text.ShouldContain("entity.Discontinue()");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    [Fact]
    public void Run_WithHandWrittenHandlerForAction_SkipsGeneratedActionHandlerAndReportsInfo()
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

                internal sealed class CustomDiscontinueProductHandler : Fluents.Requests.IHandler<DiscontinueProductRequest, ProductDto>
                {
                    public Task<IResult<ProductDto>> OnHandle(DiscontinueProductRequest request, CancellationToken cancellationToken) =>
                        throw new System.NotImplementedException();
                }
            }
            """;

        var (_, diagnostics, result) = GeneratorTestHelper.Run(DomainWithCreateAndAction, api);

        var text = GeneratedText(result);
        text.ShouldNotContain("class DiscontinueProductHandler");
        // The create request has no override; its handler still generates.
        text.ShouldContain("class CreateProductHandler");
        diagnostics.ShouldContain(d => d.Id == "DKCRUDGEN005");
    }

    [Fact]
    public void Run_WithHandWrittenHandlerUsingQualifiedRequestName_SkipsGeneratedHandler()
    {
        // Override detection must resolve the request's simple name from a namespace-qualified
        // type argument too, not just a bare identifier.
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

                internal sealed class CustomCreateProductHandler : Fluents.Requests.IHandler<MyApi.CreateProductRequest, MyApi.ProductDto>
                {
                    public Task<IResult<ProductDto>> OnHandle(CreateProductRequest request, CancellationToken cancellationToken) =>
                        throw new System.NotImplementedException();
                }
            }
            """;

        var (_, diagnostics, result) = GeneratorTestHelper.Run(DomainWithCreateAndUpdate, api);

        var text = GeneratedText(result);
        text.ShouldNotContain("class CreateProductHandler");
        diagnostics.ShouldContain(d => d.Id == "DKCRUDGEN005");
    }
}
