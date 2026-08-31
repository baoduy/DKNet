using System.Globalization;
using System.Linq;
using Shouldly;
using Xunit;

namespace SlimBus.Generators.Tests;

public class DiagnosticTests
{
    private const string EmptyApi = """
        namespace MyApi
        {
        }
        """;

    [Fact]
    public void Run_WithNoGenerateDtoForEntity_ReportsDKCRUDGEN001()
    {
        const string domain = """
            using System;
            using DKNet.EfCore.Abstractions.Attributes;
            using DKNet.EfCore.Abstractions.Entities;

            namespace MyDomain
            {
                public class Product : IEntity<Guid>
                {
                    [CrudCreate]
                    public Product(string name) => Name = name;

                    public Guid Id { get; private set; }
                    public string Name { get; private set; } = string.Empty;
                }
            }
            """;

        var (_, diagnostics, _) = GeneratorTestHelper.Run(domain, EmptyApi);

        diagnostics.ShouldContain(d => d.Id == "DKCRUDGEN001");
    }

    [Fact]
    public void Run_WithMultipleGenerateDtoForEntity_ReportsDKCRUDGEN002()
    {
        const string domain = """
            using System;
            using DKNet.EfCore.Abstractions.Attributes;
            using DKNet.EfCore.Abstractions.Entities;

            namespace MyDomain
            {
                public class Product : IEntity<Guid>
                {
                    [CrudCreate]
                    public Product(string name) => Name = name;

                    public Guid Id { get; private set; }
                    public string Name { get; private set; } = string.Empty;
                }
            }
            """;

        const string api = """
            using DKNet.EfCore.DtoGenerator;
            using MyDomain;

            namespace MyApi
            {
                [GenerateDto(typeof(Product))]
                public partial record ProductDto;

                [GenerateDto(typeof(Product))]
                public partial record ProductSummaryDto;
            }
            """;

        var (_, diagnostics, _) = GeneratorTestHelper.Run(domain, api);

        diagnostics.ShouldContain(d => d.Id == "DKCRUDGEN002");
    }

    [Fact]
    public void Run_WithMoreThanOneCrudCreateMember_ReportsDKCRUDGEN003()
    {
        const string domain = """
            using System;
            using DKNet.EfCore.Abstractions.Attributes;
            using DKNet.EfCore.Abstractions.Entities;

            namespace MyDomain
            {
                public class Product : IEntity<Guid>
                {
                    [CrudCreate]
                    public Product(string name) => Name = name;

                    [CrudCreate]
                    public static Product CreateDefault() => new Product("default");

                    public Guid Id { get; private set; }
                    public string Name { get; private set; } = string.Empty;
                }
            }
            """;

        const string api = """
            using DKNet.EfCore.DtoGenerator;
            using MyDomain;

            namespace MyApi
            {
                [GenerateDto(typeof(Product))]
                public partial record ProductDto;
            }
            """;

        var (_, diagnostics, _) = GeneratorTestHelper.Run(domain, api);

        diagnostics.ShouldContain(d => d.Id == "DKCRUDGEN003");
    }

    [Fact]
    public void Run_WithNonPublicCrudMember_ReportsDKCRUDGEN004()
    {
        const string domain = """
            using System;
            using DKNet.EfCore.Abstractions.Attributes;
            using DKNet.EfCore.Abstractions.Entities;

            namespace MyDomain
            {
                public class Product : IEntity<Guid>
                {
                    public Product(string name) => Name = name;

                    [CrudUpdate]
                    internal void Rename(string name) => Name = name;

                    public Guid Id { get; private set; }
                    public string Name { get; private set; } = string.Empty;
                }
            }
            """;

        const string api = """
            using DKNet.EfCore.DtoGenerator;
            using MyDomain;

            namespace MyApi
            {
                [GenerateDto(typeof(Product))]
                public partial record ProductDto;
            }
            """;

        var (_, diagnostics, _) = GeneratorTestHelper.Run(domain, api);

        diagnostics.ShouldContain(d => d.Id == "DKCRUDGEN004");
    }

    [Fact]
    public void Run_WithMemberMarkedBothUpdateAndAction_ReportsDKCRUDGEN007NamingTheMethod()
    {
        const string domain = """
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
                    [CrudAction]
                    public void Approve() => Status = "Approved";

                    public Guid Id { get; private set; }
                    public string Customer { get; private set; } = string.Empty;
                    public string Status { get; private set; } = string.Empty;
                }
            }
            """;

        const string api = """
            using DKNet.EfCore.DtoGenerator;
            using MyDomain;

            namespace MyApi
            {
                [GenerateDto(typeof(Order))]
                public partial record OrderDto;
            }
            """;

        var (_, diagnostics, result) = GeneratorTestHelper.Run(domain, api);

        var diagnostic = diagnostics.Single(d => d.Id == "DKCRUDGEN007");
        var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
        message.ShouldContain("Order");
        message.ShouldContain("Approve");

        // Both-annotations is never silently resolved in favour of either: the member is emitted as
        // neither an update nor an action (spec §3.8 / R4).
        var text = string.Join("\n", result.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));
        text.ShouldNotContain("ApproveOrderRequest");
    }

    [Fact]
    public void Run_WithUpdateAndActionSegmentCollision_ReportsDKCRUDGEN008NamingBothMembers()
    {
        const string domain = """
            using System;
            using DKNet.EfCore.Abstractions.Attributes;
            using DKNet.EfCore.Abstractions.Entities;

            namespace MyDomain
            {
                public class Product : IEntity<Guid>
                {
                    [CrudCreate]
                    public Product(string name) => Name = name;

                    [CrudUpdate]
                    public void ChangePrice(decimal price) { }

                    [CrudUpdate]
                    public void Discontinue() => Name = "(discontinued)";

                    [CrudAction("discontinue")]
                    public void Retire() => Name = "(retired)";

                    public Guid Id { get; private set; }
                    public string Name { get; private set; } = string.Empty;
                }
            }
            """;

        const string api = """
            using DKNet.EfCore.DtoGenerator;
            using MyDomain;

            namespace MyApi
            {
                [GenerateDto(typeof(Product))]
                public partial record ProductDto;
            }
            """;

        var (_, diagnostics, _) = GeneratorTestHelper.Run(domain, api);

        var diagnostic = diagnostics.Single(d => d.Id == "DKCRUDGEN008");
        var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
        message.ShouldContain("Discontinue");
        message.ShouldContain("Retire");
        message.ShouldContain("discontinue");
    }

    [Fact]
    public void Run_WithTwoActionsSegmentCollision_ReportsDKCRUDGEN008RegardlessOfVerb()
    {
        // Spec Appendix B.3: the §5 collision scenario only pairs an action against an update member; two
        // actions colliding must fire the same diagnostic, and it is an error irrespective of the verbs
        // involved (here POST vs. PATCH).
        const string domain = """
            using System;
            using DKNet.EfCore.Abstractions.Attributes;
            using DKNet.EfCore.Abstractions.Entities;

            namespace MyDomain
            {
                public class Product : IEntity<Guid>
                {
                    [CrudCreate]
                    public Product(string name) => Name = name;

                    [CrudAction("archive")]
                    public void Discontinue() => Name = "(discontinued)";

                    [CrudAction("archive", Verb = CrudActionVerb.Patch)]
                    public void Retire() => Name = "(retired)";

                    public Guid Id { get; private set; }
                    public string Name { get; private set; } = string.Empty;
                }
            }
            """;

        const string api = """
            using DKNet.EfCore.DtoGenerator;
            using MyDomain;

            namespace MyApi
            {
                [GenerateDto(typeof(Product))]
                public partial record ProductDto;
            }
            """;

        var (_, diagnostics, _) = GeneratorTestHelper.Run(domain, api);

        var diagnostic = diagnostics.Single(d => d.Id == "DKCRUDGEN008");
        var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
        message.ShouldContain("Discontinue");
        message.ShouldContain("Retire");
        message.ShouldContain("archive");
    }

    [Fact]
    public void Run_WithEntityNotImplementingIEntity_ReportsDKCRUDGEN006()
    {
        const string domain = """
            using DKNet.EfCore.Abstractions.Attributes;

            namespace MyDomain
            {
                public class Product
                {
                    [CrudCreate]
                    public Product(string name) => Name = name;

                    public string Name { get; private set; } = string.Empty;
                }
            }
            """;

        const string api = """
            using DKNet.EfCore.DtoGenerator;
            using MyDomain;

            namespace MyApi
            {
                [GenerateDto(typeof(Product))]
                public partial record ProductDto;
            }
            """;

        var (_, diagnostics, _) = GeneratorTestHelper.Run(domain, api);

        diagnostics.ShouldContain(d => d.Id == "DKCRUDGEN006");
    }
}
