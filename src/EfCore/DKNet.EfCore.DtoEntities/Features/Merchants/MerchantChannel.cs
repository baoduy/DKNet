using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.DtoEntities.Features.StaticData;
using DKNet.EfCore.DtoEntities.Share;

namespace DKNet.EfCore.DtoEntities.Features.Merchants;

[AuditLog]
public sealed class MerchantChannel : ChannelDataBase
{
    #region Constructors

    public MerchantChannel(Guid merchantId, ChannelCodes code, string settlement, decimal minAmount, decimal? maxAmount,
        string byUser) : base(code, settlement, minAmount, maxAmount, byUser) =>
        MerchantId = merchantId;

    private MerchantChannel(string createdBy) : base(ChannelCodes.None, string.Empty, 0, null, createdBy)
    {
    }

    #endregion

    #region Properties

    public Merchant Merchant { get; private set; } = null!;
    public Guid MerchantId { get; private set; }

    #endregion
}