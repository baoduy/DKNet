using DKNet.EfCore.DtoEntities;

namespace DKNet.EfCore.DtoGenerator.Tests.Features;

[GenerateDto(typeof(TestProduct))]
public partial record TestProductDto;