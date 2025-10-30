using DKNet.EfCore.DtoEntities.Features.Merchants;
using DKNet.EfCore.DtoGenerator;

namespace EfCore.DtoGenerator.Tests.Features.Merchants;

// Include all properties except Merchant, and explicitly include audit properties to override global exclusions
[GenerateDto(
    typeof(MerchantChannel),
    Include =
    [
        nameof(MerchantChannel.Id),
        nameof(MerchantChannel.MerchantId),
        nameof(MerchantChannel.Code),
        nameof(MerchantChannel.Settlement),
        nameof(MerchantChannel.MinAmount),
        nameof(MerchantChannel.MaxAmount),
        nameof(MerchantChannel.CreatedBy),
        nameof(MerchantChannel.CreatedOn),
        nameof(MerchantChannel.LastModifiedBy),
        nameof(MerchantChannel.LastModifiedOn)
    ])]
public partial record MerchantChannelDto;