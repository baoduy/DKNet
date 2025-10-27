using DKNet.EfCore.DtoEntities;

namespace DKNet.EfCore.DtoGenerator.Tests.Features;

// Test DTO without IgnoreComplexType flag - should include all properties including complex types
[GenerateDto(typeof(Customer))]
public partial record CustomerDto;