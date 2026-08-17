using DKNet.EfCore.DtoEntities.Features.Merchants;
using DKNet.EfCore.DtoGenerator;

namespace EfCore.DtoGenerator.Tests.Features.Merchants;

[GenerateDto(typeof(MerchantBalance), IgnoreComplexType = false)]
public partial record MerchantBalanceDto;