using DKNet.EfCore.DtoEntities.Features.Merchants;

namespace DKNet.EfCore.DtoGenerator.Tests.Features.Merchants;

[GenerateDto(typeof(MerchantChannel), Exclude = [nameof(MerchantChannel.Merchant)])]
public partial record MerchantChannelDto;