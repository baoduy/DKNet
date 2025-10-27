using DKNet.EfCore.DtoEntities;
using DKNet.EfCore.DtoGenerator;

namespace EfCore.DtoGenerator.Tests.Features;

// Test DTO without IgnoreComplexType flag - should include all properties including complex types
[GenerateDto(typeof(Customer))]
public partial record CustomerDto;