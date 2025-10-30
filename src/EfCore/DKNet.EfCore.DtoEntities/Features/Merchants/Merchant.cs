using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.DtoEntities.Share;

namespace DKNet.EfCore.DtoEntities.Features.Merchants;

public enum MerchantStatus
{
    None,
    Pending,
    Active,
    Inactive,
    Suspended
}

[AuditLog]
public sealed class Merchant : AggregateRoot, ICodeEntity
{
    #region Fields

    private readonly HashSet<MerchantChannel> _channels = [];
    private readonly HashSet<MerchantBalance> _merchantBalances = [];

    #endregion

    #region Constructors

    public Merchant(
        string name,
        string email,
        string? description,
        string countryCode,
        string defaultCurrency,
        string byUser) : this(Guid.CreateVersion7(), name, email, description, countryCode, defaultCurrency, byUser)
    {
    }

    private Merchant(
        Guid id,
        string name,
        string email,
        string? description,
        string countryCode,
        string defaultCurrency,
        string byUser) : base(id, byUser)

    {
        this.Code = id.ToString();
        this.Name = name;
        this.Email = email;
        this.CountryCode = countryCode;
        this.DefaultCurrency = defaultCurrency;
        this.Description = description;
        this.MxFee = MxFee.Default;
    }

    private Merchant(Guid id, string createdBy) : base(id, createdBy)
    {
        this.Code = string.Empty;
        this.Name = string.Empty;
        this.CountryCode = string.Empty;
        this.DefaultCurrency = string.Empty;
        this.Description = string.Empty;
        this.Email = string.Empty;
        this.MxFee = MxFee.Default;
    }

    #endregion

    #region Properties

    public ClientAppInfo? ClientAppInfo { get; private set; }

    public DateTimeOffset? ApprovedOn { get; private set; }

    public IReadOnlyCollection<MerchantBalance> MerchantBalances => this._merchantBalances;

    public IReadOnlyCollection<MerchantChannel> Channels => this._channels;

    public LoginInfo? LoginInfo { get; private set; }

    public MerchantStatus Status { get; private set; } = MerchantStatus.Pending;

    public MxFee MxFee { get; private set; }

    public string Code { get; private set; }

    [MaxLength(3)] public string CountryCode { get; private set; }

    [MaxLength(3)] public string DefaultCurrency { get; private set; }

    [MaxLength(100)] public string Email { get; private set; }

    [MaxLength(100)] public string Name { get; private set; }

    [Display(Name = "Approved By")]
    [MaxLength(100)]
    public string? ApprovedBy { get; private set; }

    [MaxLength(550)] public string? Description { get; private set; }

    public WebHookInfo? WebHook { get; private set; }

    #endregion

    #region Methods

    public void Approve(string byUser)
    {
        this.Status = MerchantStatus.Active;
        this.ApprovedBy = byUser;
        this.ApprovedOn = DateTimeOffset.Now;
        this.SetUpdatedBy(byUser);
    }

    private MerchantBalance GetOrCreateBalance(string currency, string byUser)
    {
        var balance = this._merchantBalances.FirstOrDefault(b =>
            string.Equals(b.Currency, currency, StringComparison.OrdinalIgnoreCase));
        if (balance is not null)
        {
            return balance;
        }

        balance = MerchantBalance.Create(this.Id, this, currency, byUser);
        this._merchantBalances.Add(balance);

        return balance;
    }

    public void LinkClientAppInfo(ClientAppInfo info, string byUser)
    {
        this.ClientAppInfo = info;
        this.SetUpdatedBy(byUser);
    }

    public void LinkLoginInfo(LoginInfo loginInfo, string byUser)
    {
        this.LoginInfo = loginInfo;
        this.SetUpdatedBy(byUser);
    }

    public void SetActivationStatus(bool disabled, string byUser)
    {
        this.Status = disabled ? MerchantStatus.Inactive : MerchantStatus.Active;
        this.SetUpdatedBy(byUser);
    }

    public void Update(
        string? name,
        string? email,
        string? description,
        string? countryCode,
        string? defaultCurrency,
        string byUser)
    {
        if (!string.IsNullOrEmpty(name))
        {
            this.Name = name;
        }

        if (!string.IsNullOrEmpty(email))
        {
            this.Email = email;
        }

        if (!string.IsNullOrEmpty(description))
        {
            this.Description = description;
        }

        if (!string.IsNullOrEmpty(countryCode))
        {
            this.CountryCode = countryCode;
        }

        if (!string.IsNullOrEmpty(defaultCurrency))
        {
            this.DefaultCurrency = defaultCurrency;
        }

        this.SetUpdatedBy(byUser);
    }

    public void UpdateFee(MxFee mxFee, string byUser)
    {
        this.MxFee = mxFee;
        this.SetUpdatedBy(byUser);
    }

    public void UpdateWebHook(WebHookInfo webHookInfo, string byUser)
    {
        this.WebHook = webHookInfo;
        this.SetUpdatedBy(byUser);
    }

    #endregion
}