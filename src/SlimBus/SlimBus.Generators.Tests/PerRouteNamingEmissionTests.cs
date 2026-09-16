using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace SlimBus.Generators.Tests;

/// <summary>
///     DRK-1327 §5 scenario 5 (@unit): "A route name is the same every time it is generated." Runs the real
///     generator twice over an unchanged entity declaring two update routes ("Rename" and "ChangePrice",
///     matching the spec's own Product example verbatim) and compares the emitted text.
/// </summary>
public class PerRouteNamingEmissionTests
{
    private const string DomainWithRenameAndChangePrice = """
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
                public void Rename(string name) => Name = name;

                [CrudUpdate]
                public void ChangePrice(decimal price) => Price = price;

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
    public void Run_TwiceOverTheSameEntity_ProducesTheSameRouteNamesBothTimes()
    {
        var (_, _, firstResult) = GeneratorTestHelper.Run(DomainWithRenameAndChangePrice, ApiWithProductDto);
        var (_, _, secondResult) = GeneratorTestHelper.Run(DomainWithRenameAndChangePrice, ApiWithProductDto);

        var firstText = GeneratorTestHelper.GeneratedText(firstResult);
        var secondText = GeneratorTestHelper.GeneratedText(secondResult);

        // R7: generation is deterministic — the same source always emits the same text.
        secondText.ShouldBe(firstText);

        // The route-naming rule (R1/R2) itself: every route's name — the enum member's own name for
        // GetById/GetList/Create/Delete, the method's own name for Update/Action — is validated before any
        // route is mapped, ordinally, so a misspelt name is reported (DRK-1327 §5 scenario 4).
        const string expectedCall =
            "options.ValidateRouteNames(\"Product\", \"GetById\", \"GetList\", \"Create\", \"Delete\", \"Rename\", \"ChangePrice\");";
        firstText.ShouldContain(expectedCall);
    }
}
