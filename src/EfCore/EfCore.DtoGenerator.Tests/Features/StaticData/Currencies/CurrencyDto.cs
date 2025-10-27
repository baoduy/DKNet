using DKNet.EfCore.DtoEntities.Features.StaticData;
using DKNet.EfCore.DtoGenerator;

namespace EfCore.DtoGenerator.Tests.Features.StaticData.Currencies;

[GenerateDto(typeof(CurrencyData))]
public partial record CurrencyDto;