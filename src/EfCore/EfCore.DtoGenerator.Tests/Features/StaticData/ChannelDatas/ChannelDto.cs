using DKNet.EfCore.DtoEntities.Features.StaticData;
using DKNet.EfCore.DtoGenerator;

namespace EfCore.DtoGenerator.Tests.Features.StaticData.ChannelDatas;

// Include audit properties explicitly to override global exclusions for this DTO
[GenerateDto(typeof(ChannelData), Include =
[
    nameof(ChannelData.Id),
    nameof(ChannelData.Code),
    nameof(ChannelData.Country),
    nameof(ChannelData.Currency),
    nameof(ChannelData.Name),
    nameof(ChannelData.Settlement),
    nameof(ChannelData.MinAmount),
    nameof(ChannelData.MaxAmount),
    nameof(ChannelData.CreatedBy),
    nameof(ChannelData.CreatedOn),
    nameof(ChannelData.LastModifiedBy),
    nameof(ChannelData.LastModifiedOn)
])]
public partial record ChannelDto;