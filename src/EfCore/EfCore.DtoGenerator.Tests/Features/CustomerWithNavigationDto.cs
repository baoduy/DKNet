using DKNet.EfCore.DtoEntities;
using DKNet.EfCore.DtoGenerator;

namespace EfCore.DtoGenerator.Tests.Features;

// Test DTO with IgnoreComplexType explicitly set to false - should include navigation properties (Orders and PrimaryAddress)
[GenerateDto(typeof(Customer), IgnoreComplexType = false)]
public partial record CustomerWithNavigationDto;