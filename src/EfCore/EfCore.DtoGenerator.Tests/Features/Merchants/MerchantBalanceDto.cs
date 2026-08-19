using EfCore.DtoGenerator.TestEntities.Features.Merchants;
using DKNet.EfCore.DtoGenerator;

namespace EfCore.DtoGenerator.Tests.Features.Merchants;

[GenerateDto(typeof(MerchantBalance), IgnoreComplexType = false)]
public partial record MerchantBalanceDto;