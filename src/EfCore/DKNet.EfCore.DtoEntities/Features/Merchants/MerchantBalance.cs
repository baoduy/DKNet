using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.DtoEntities.Share;

namespace DKNet.EfCore.DtoEntities.Features.Merchants;

public sealed class MerchantBalance : DomainEntity, IBalanceAmounts
{
    #region Constructors

    private MerchantBalance(
        Guid merchantId,
        Merchant merchant,
        string currency,
        string byUser)
        : base(byUser)
    {
        Currency = currency.ToUpperInvariant();
        MerchantId = merchantId;
        Merchant = merchant;
    }

    private MerchantBalance(Guid id, string createdBy) : base(id, createdBy)
    {
        Currency = string.Empty;
        MerchantId = Guid.Empty;
        Merchant = null!;
    }

    #endregion

    #region Properties

    //public decimal TotalAmount { get; private set; }
    public decimal AvailableAmount { get; private set; }

    public int AvailableCount { get; private set; }

    [MaxLength(3)] public string Currency { get; private set; }

    public decimal FeeAmount { get; private set; }

    public Merchant Merchant { get; private set; }

    public Guid MerchantId { get; private set; }

    public decimal PendingAmount { get; private set; }

    public int PendingCount { get; private set; }

    public decimal ReservedAmount { get; private set; }

    #endregion

    #region Methods

    public static MerchantBalance Create(
        Guid merchantId,
        Merchant merchant,
        string currency,
        string byUser) =>
        new(
            merchantId,
            merchant,
            currency,
            byUser);

    public void SetAvailable(int count, decimal amount)
    {
        AvailableAmount = amount;
        AvailableCount = count;
    }

    public void SetPending(int count, decimal amount)
    {
        PendingAmount = amount;
        PendingCount = count;
    }

    public void SetReservedAmount(decimal amount)
    {
        ReservedAmount = amount;
    }

    #endregion
}