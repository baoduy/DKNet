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
