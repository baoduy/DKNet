using Shouldly;
using Xunit;

namespace SlimBus.Generators.Tests;

/// <summary>
/// Covers <see cref="GeneratorTestHelper.GeneratedText"/> itself, not generator behaviour — the newline
/// normalisation it performs is invisible on <c>ubuntu-latest</c> CI, so it needs its own guard rather than
/// relying on an assertion elsewhere happening to fail on Windows.
/// </summary>
public class GeneratorTestHelperTests
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

    [Fact]
    public void GeneratedText_OnAnyRunResult_ContainsNoCarriageReturns()
    {
        var (_, _, result) = GeneratorTestHelper.Run(DomainWithCreateCtor, ApiWithProductDto);

        var text = GeneratorTestHelper.GeneratedText(result);

        text.ShouldNotContain("\r");
    }
}
