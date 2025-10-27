using DKNet.EfCore.DtoEntities;

namespace DKNet.EfCore.DtoGenerator.Tests.Features;

// Test DTO with IgnoreComplexType flag set to true - should exclude Orders and PrimaryAddress
[GenerateDto(typeof(Customer), IgnoreComplexType = true)]
public partial record CustomerSimpleDto;