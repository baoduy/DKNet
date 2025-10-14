using DKNet.EfCore.DtoEntities;

namespace DKNet.EfCore.DtoGenerator.Tests;

// PersonDto - generates all properties from Person entity
[GenerateDto(typeof(Person))]
public partial record PersonDto;

// CurrencyDataDto - using unqualified typeof for CurrencyData from different namespace
[GenerateDto(typeof(DtoEntities.Features.StaticData.CurrencyData))]
public partial record CurrencyDataDto;

// PersonBasicDto - excludes some properties using collection expression
[GenerateDto(typeof(Person), Exclude = ["MiddleName", "Age", "CreatedUtc"])]
public partial record PersonBasicDto;

// CustomerDto - has cross-namespace references and collections
[GenerateDto(typeof(Customer))]
public partial record CustomerDto;

// OrderDto - has collection with empty initializer
[GenerateDto(typeof(Order))]
public partial record OrderDto;

// PersonCustomDto - has an existing property that should not be duplicated
[GenerateDto(typeof(Person))]
public partial record PersonCustomDto
{
    // FirstName is manually defined here, so it should not be generated again
    public required string FirstName { get; init; } = default!;
}

