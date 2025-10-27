using DKNet.EfCore.DtoEntities;
using DKNet.EfCore.DtoGenerator;

namespace EfCore.DtoGenerator.Tests.Features;

// Test DTO with IgnoreComplexType and Exclude - should exclude complex types AND specified properties
[GenerateDto(typeof(Customer), IgnoreComplexType = true, Exclude = ["Email"])]
public partial record CustomerWithExcludeDto;