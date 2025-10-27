using DKNet.EfCore.DtoEntities.Features.Merchants;
using DKNet.EfCore.DtoGenerator;

namespace EfCore.DtoGenerator.Tests.Features.Merchants;

[GenerateDto(typeof(MerchantBalance))]
public partial record MerchantBalanceDto;