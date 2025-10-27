using DKNet.EfCore.DtoEntities;
using DKNet.EfCore.DtoGenerator;

namespace EfCore.DtoGenerator.Tests.Features;

[GenerateDto(typeof(TestProduct))]
public partial record TestProductDto;