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

    [Fact]
    public void Run_WithNoActions_EmitsUnchangedCreateReadListDeleteAndUpdateRegistrations()
    {
        // Spec Appendix B.2: the only existing-behaviour Gherkin scenario covers update routing; this closes
        // the gap by asserting the FULL slice (create/read/list/delete too) for an entity declaring no actions
        // is byte-identical to today's, and that no action registration appears at all.
        var (output, _, result) = GeneratorTestHelper.Run(DomainWithCreateAndTwoUpdates, ApiWithProductDto);

        var text = GeneratedText(result);
        text.ShouldContain("group.MapGetById<global::MyDomain.Product, global::System.Guid, global::MyApi.ProductDto>();");
        text.ShouldContain("group.MapGetList<global::MyDomain.Product, global::System.Guid, global::MyApi.ProductDto>();");
        text.ShouldContain("group.MapDeleteById<global::MyDomain.Product, global::System.Guid>();");
        text.ShouldContain("group.MapPost<CreateProductRequest, global::MyApi.ProductDto>(\"/\");");
        text.ShouldContain("group.MapPutById<UpdatePriceProductRequest, global::System.Guid, global::MyApi.ProductDto>(\"{id}\");");
        text.ShouldContain("group.MapPutById<UpdateNameProductRequest, global::System.Guid, global::MyApi.ProductDto>(\"{id}/update-name\");");
        text.ShouldNotContain("MapActionById");
        text.ShouldNotContain("CrudOp.Action");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    private const string DomainWithCreateUpdateAndExplicitRouteAction = """
        using System;
        using System.ComponentModel.DataAnnotations;
        using DKNet.EfCore.Abstractions.Attributes;
        using DKNet.EfCore.Abstractions.Entities;

        namespace MyDomain
        {
            public class Order : IEntity<Guid>
            {
                [CrudCreate]
                public Order(string customer) => Customer = customer;

                [CrudUpdate]
                public void ChangeStatus(string status) => Status = status;

                [CrudAction("approval")]
                public void Approve([Required, StringLength(50)] string approver) => Approver = approver;

                public Guid Id { get; private set; }
                public string Customer { get; private set; } = string.Empty;
                public string Status { get; private set; } = string.Empty;
                public string? Approver { get; private set; }
            }
        }
        """;

    private const string ApiWithOrderDto = """
        using DKNet.EfCore.DtoGenerator;
        using MyDomain;

        namespace MyApi
        {
            [GenerateDto(typeof(Order))]
            public partial record OrderDto;
        }
        """;

    [Fact]
    public void Run_WithExplicitActionRoute_RegistersPostAtTheChosenSegment()
    {
        var (output, _, result) = GeneratorTestHelper.Run(DomainWithCreateUpdateAndExplicitRouteAction, ApiWithOrderDto);

        var text = GeneratedText(result);
        text.ShouldContain(
            "group.MapActionById<ApproveOrderRequest, global::System.Guid, global::MyApi.OrderDto>(\"{id}/approval\", \"POST\");");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    private const string DomainWithActionDefaultingSegment = """
        using System;
        using DKNet.EfCore.Abstractions.Attributes;
        using DKNet.EfCore.Abstractions.Entities;

        namespace MyDomain
        {
            public class Order : IEntity<Guid>
            {
                [CrudCreate]
                public Order(string customer) => Customer = customer;

                [CrudAction]
                public void RejectOrder(string reason) => Status = reason;

                public Guid Id { get; private set; }
                public string Customer { get; private set; } = string.Empty;
                public string Status { get; private set; } = string.Empty;
            }
        }
        """;

    [Fact]
    public void Run_WithActionAndNoExplicitRoute_DefaultsSegmentToKebabCaseMethodName()
    {
        var (output, _, result) = GeneratorTestHelper.Run(DomainWithActionDefaultingSegment, ApiWithOrderDto);

        var text = GeneratedText(result);
        text.ShouldContain(
            "group.MapActionById<RejectOrderOrderRequest, global::System.Guid, global::MyApi.OrderDto>(\"{id}/reject-order\", \"POST\");");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    private const string DomainWithPatchAction = """
        using System;
        using DKNet.EfCore.Abstractions.Attributes;
        using DKNet.EfCore.Abstractions.Entities;

        namespace MyDomain
        {
            public class Order : IEntity<Guid>
            {
                [CrudCreate]
                public Order(string customer) => Customer = customer;

                [CrudAction(Verb = CrudActionVerb.Patch)]
                public void Archive() => Status = "Archived";

                public Guid Id { get; private set; }
                public string Customer { get; private set; } = string.Empty;
                public string Status { get; private set; } = string.Empty;
            }
        }
        """;

    [Fact]
    public void Run_WithActionVerbOverriddenToPatch_RegistersPatchAtDefaultSegment()
    {
        var (output, _, result) = GeneratorTestHelper.Run(DomainWithPatchAction, ApiWithOrderDto);

        var text = GeneratedText(result);
        text.ShouldContain(
            "group.MapActionById<ArchiveOrderRequest, global::System.Guid, global::MyApi.OrderDto>(\"{id}/archive\", \"PATCH\");");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    private const string DomainWithPutActionAndUpdateMember = """
        using System;
        using DKNet.EfCore.Abstractions.Attributes;
        using DKNet.EfCore.Abstractions.Entities;

        namespace MyDomain
        {
            public class Order : IEntity<Guid>
            {
                [CrudCreate]
                public Order(string customer) => Customer = customer;

                [CrudUpdate]
                public void ChangeStatus(string status) => Status = status;

                [CrudAction(Verb = CrudActionVerb.Put)]
                public void Reinstate() => Status = "Active";

                public Guid Id { get; private set; }
                public string Customer { get; private set; } = string.Empty;
                public string Status { get; private set; } = string.Empty;
            }
        }
        """;

    [Fact]
    public void Run_WithPutActionAndUpdateMember_ActionNeverClaimsThePlainByIdRoute()
    {
        var (output, _, result) = GeneratorTestHelper.Run(DomainWithPutActionAndUpdateMember, ApiWithOrderDto);

        var text = GeneratedText(result);
        text.ShouldContain(
            "group.MapActionById<ReinstateOrderRequest, global::System.Guid, global::MyApi.OrderDto>(\"{id}/reinstate\", \"PUT\");");
        text.ShouldContain(
            "group.MapPutById<ChangeStatusOrderRequest, global::System.Guid, global::MyApi.OrderDto>(\"{id}\");");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    private const string DomainWithSoleAction = """
        using System;
        using DKNet.EfCore.Abstractions.Attributes;
        using DKNet.EfCore.Abstractions.Entities;

        namespace MyDomain
        {
            public class Order : IEntity<Guid>
            {
                [CrudCreate]
                public Order(string customer) => Customer = customer;

                [CrudAction]
                public void Approve() => Status = "Approved";

                public Guid Id { get; private set; }
                public string Customer { get; private set; } = string.Empty;
                public string Status { get; private set; } = string.Empty;
            }
        }
        """;

    [Fact]
    public void Run_WithSoleActionAndNoUpdateMembers_NeverRegistersThePlainByIdRoute()
    {
        var (output, _, result) = GeneratorTestHelper.Run(DomainWithSoleAction, ApiWithOrderDto);

        var text = GeneratedText(result);
        text.ShouldContain(
            "group.MapActionById<ApproveOrderRequest, global::System.Guid, global::MyApi.OrderDto>(\"{id}/approve\", \"POST\");");
        text.ShouldNotContain("MapPutById");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    private const string DomainWithActionThenUpdate = """
        using System;
        using DKNet.EfCore.Abstractions.Attributes;
        using DKNet.EfCore.Abstractions.Entities;

        namespace MyDomain
        {
            public class Order : IEntity<Guid>
            {
                [CrudCreate]
                public Order(string customer) => Customer = customer;

                [CrudAction]
                public void Approve() => Status = "Approved";

                [CrudUpdate]
                public void ChangeStatus(string status) => Status = status;

                public Guid Id { get; private set; }
                public string Customer { get; private set; } = string.Empty;
                public string Status { get; private set; } = string.Empty;
            }
        }
        """;

    private const string DomainWithUpdateThenAction = """
        using System;
        using DKNet.EfCore.Abstractions.Attributes;
        using DKNet.EfCore.Abstractions.Entities;

        namespace MyDomain
        {
            public class Order : IEntity<Guid>
            {
                [CrudCreate]
                public Order(string customer) => Customer = customer;

                [CrudUpdate]
                public void ChangeStatus(string status) => Status = status;

                [CrudAction]
                public void Approve() => Status = "Approved";

                public Guid Id { get; private set; }
                public string Customer { get; private set; } = string.Empty;
                public string Status { get; private set; } = string.Empty;
            }
        }
        """;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Run_WithActionAndUpdateDeclaredInEitherOrder_RouteResolutionIsOrderIndependent(bool actionDeclaredFirst)
    {
        var domain = actionDeclaredFirst ? DomainWithActionThenUpdate : DomainWithUpdateThenAction;

        var (output, _, result) = GeneratorTestHelper.Run(domain, ApiWithOrderDto);

        var text = GeneratedText(result);
        text.ShouldContain(
            "group.MapActionById<ApproveOrderRequest, global::System.Guid, global::MyApi.OrderDto>(\"{id}/approve\", \"POST\");");
        text.ShouldContain(
            "group.MapPutById<ChangeStatusOrderRequest, global::System.Guid, global::MyApi.OrderDto>(\"{id}\");");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }
}
