using Some.Others.Namespaces;

namespace DKNet.EfCore.DtoGenerator.Tests;

[GenerateDto(typeof(Person))]
public partial record PersonDto;

[GenerateDto(typeof(Customer))]
public partial record CustomerDto;

[GenerateDto(typeof(Order))]
public partial record OrderDto;

[GenerateDto(typeof(OrderItem))]
public partial record OrderItemDto;

[GenerateDto(typeof(Person))]
public partial record PersonCustomDto
{
    // We intentionally provide our own FirstName; generator should skip generating a second one
    public string FirstName { get; init; } = string.Empty;

    // Add an extra property not present on entity to confirm it stays
    public string DisplayName => FirstName + "?"; // expression-bodied, not init-only; still fine
}

// Test DTOs with Exclude feature
[GenerateDto(typeof(Person), Exclude = ["Id", "CreatedUtc"])]
public partial record PersonSummaryDto;

[GenerateDto(typeof(Customer), Exclude = ["CustomerId"])]
public partial record CustomerPublicDto;

[GenerateDto(typeof(Order), Exclude = ["Id", "OrderedUtc"])]
public partial record OrderSummaryDto;

[GenerateDto(typeof(Person), Exclude = ["MiddleName", "Age", "CreatedUtc"])]
public partial record PersonBasicDto;

// Intentionally omit using External.Library.Domain; rely on simple name resolution fallback
[GenerateDto(typeof(CurrencyData))]
public partial record CurrencyDataDto;