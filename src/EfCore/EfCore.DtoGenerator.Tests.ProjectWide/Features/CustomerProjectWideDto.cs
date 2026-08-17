using DKNet.EfCore.DtoEntities;
using DKNet.EfCore.DtoGenerator;

namespace EfCore.DtoGenerator.Tests.ProjectWide.Features;

// No explicit IgnoreComplexType flag - this project sets DtoGeneratorIgnoreComplexType=false,
// so navigation properties (Orders, PrimaryAddress) are INCLUDED by default
[GenerateDto(typeof(Customer))]
public partial record CustomerProjectWideDto;