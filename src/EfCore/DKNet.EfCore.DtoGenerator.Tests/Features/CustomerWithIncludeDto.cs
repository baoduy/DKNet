using DKNet.EfCore.DtoEntities;

namespace DKNet.EfCore.DtoGenerator.Tests.Features;

// Test DTO with Include parameter - should ignore IgnoreComplexType flag and only include specified properties
[GenerateDto(typeof(Customer), IgnoreComplexType = true, Include = ["CustomerId", "Name", "Orders"])]
public partial record CustomerWithIncludeDto;