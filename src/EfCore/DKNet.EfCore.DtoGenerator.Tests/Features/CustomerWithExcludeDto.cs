using DKNet.EfCore.DtoEntities;

namespace DKNet.EfCore.DtoGenerator.Tests.Features;

// Test DTO with IgnoreComplexType and Exclude - should exclude complex types AND specified properties
[GenerateDto(typeof(Customer), IgnoreComplexType = true, Exclude = ["Email"])]
public partial record CustomerWithExcludeDto;