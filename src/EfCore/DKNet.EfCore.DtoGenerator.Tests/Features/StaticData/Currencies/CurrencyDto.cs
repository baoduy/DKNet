using DKNet.EfCore.DtoEntities.Features.StaticData;

namespace DKNet.EfCore.DtoGenerator.Tests.Features.StaticData.Currencies;

[GenerateDto(typeof(CurrencyData))]
public partial record CurrencyDto;