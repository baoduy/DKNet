using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.DtoEntities.Share;

namespace DKNet.EfCore.DtoEntities.Features.StaticData;

public abstract class ChannelDataBase : AuditedEntity<int>
{
    #region Constructors

    protected ChannelDataBase(ChannelCodes code, string settlement, decimal minAmount, decimal? maxAmount,
        string byUser)
    {
        Code = code;
        MaxAmount = maxAmount;
        MinAmount = minAmount;
        Settlement = settlement;

        SetCreatedBy(byUser);
        if (!IsValidSettlementFormat(settlement))
            throw new ArgumentException("Invalid settlement format. It should T+Date format.", nameof(settlement));
    }

    #endregion

    #region Properties

    [MaxLength(10)] public ChannelCodes Code { get; private set; }

    public decimal? MaxAmount { get; private set; }
    public decimal MinAmount { get; private set; }

    public string Settlement { get; private set; }

    #endregion

    #region Methods

    private static bool IsValidSettlementFormat(string settlement) => Regex.IsMatch(settlement, @"^T\+\d+$",
        RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));

    #endregion
}

[AuditLog]
public sealed class ChannelData : ChannelDataBase
{
    #region Constructors

    private ChannelData(ChannelCodes code, string name, string country, string currency, string settlement,
        decimal minAmount,
        decimal? maxAmount, string byUser) : base(code, settlement, minAmount, maxAmount, byUser)
    {
        Country = country;
        Currency = currency;
        Name = name;
    }

    private ChannelData() : base(ChannelCodes.None, string.Empty, 0, null, string.Empty)
    {
        Country = string.Empty;
        Currency = string.Empty;
        Name = string.Empty;
    }

    #endregion

    #region Properties

    [MaxLength(3)] public string Country { get; private set; }
    [MaxLength(4)] public string Currency { get; private set; }
    [MaxLength(50)] public string Name { get; private set; }

    #endregion

    #region Methods

    public static ChannelData Create(ChannelCodes code, string name, string settlement, string country, string currency,
        decimal minAmount, decimal? maxAmount, string byUser) =>
        new(code, name, country, currency, settlement, minAmount, maxAmount, byUser);

    #endregion
}