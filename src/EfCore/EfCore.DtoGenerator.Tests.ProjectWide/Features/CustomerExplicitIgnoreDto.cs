using EfCore.DtoGenerator.TestEntities;
using DKNet.EfCore.DtoGenerator;

namespace EfCore.DtoGenerator.Tests.ProjectWide.Features;

// Explicit per-DTO flag overrides the project-wide DtoGeneratorIgnoreComplexType=false:
// navigation properties are EXCLUDED for this DTO
[GenerateDto(typeof(Customer), IgnoreComplexType = true)]
public partial record CustomerExplicitIgnoreDto;