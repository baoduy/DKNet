using DKNet.EfCore.DtoEntities;
using DKNet.EfCore.DtoGenerator;

namespace EfCore.DtoGenerator.Tests.Features;

// Test DTO with IgnoreComplexType flag set to true - should exclude Orders and PrimaryAddress
[GenerateDto(typeof(Customer), IgnoreComplexType = true)]
public partial record CustomerSimpleDto;