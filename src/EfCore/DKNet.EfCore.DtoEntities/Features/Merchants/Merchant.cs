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
        Code = id.ToString();
        Name = name;
        Email = email;
        CountryCode = countryCode;
        DefaultCurrency = defaultCurrency;
        Description = description;
        MxFee = MxFee.Default;
    }

    private Merchant(Guid id, string createdBy) : base(id, createdBy)
    {
        Code = string.Empty;
        Name = string.Empty;
        CountryCode = string.Empty;
        DefaultCurrency = string.Empty;
        Description = string.Empty;
        Email = string.Empty;
        MxFee = MxFee.Default;
    }

    #endregion

    #region Properties

    [Display(Name = "Approved By")]
    [MaxLength(100)]
    public string? ApprovedBy { get; private set; }

    public DateTimeOffset? ApprovedOn { get; private set; }

    public IReadOnlyCollection<MerchantChannel> Channels => _channels;

    public ClientAppInfo? ClientAppInfo { get; private set; }

    public string Code { get; private set; }

    [MaxLength(3)] public string CountryCode { get; private set; }

    [MaxLength(3)] public string DefaultCurrency { get; private set; }

    [MaxLength(550)] public string? Description { get; private set; }

    [MaxLength(100)] public string Email { get; private set; }

    public LoginInfo? LoginInfo { get; private set; }

    public IReadOnlyCollection<MerchantBalance> MerchantBalances => _merchantBalances;

    public MxFee MxFee { get; private set; }

    [MaxLength(100)] public string Name { get; private set; }

    public MerchantStatus Status { get; private set; } = MerchantStatus.Pending;

    public WebHookInfo? WebHook { get; private set; }

    #endregion

    #region Methods

    public void Approve(string byUser)
    {
        Status = MerchantStatus.Active;
        ApprovedBy = byUser;
        ApprovedOn = DateTimeOffset.Now;
        SetUpdatedBy(byUser);
    }

    private MerchantBalance GetOrCreateBalance(string currency, string byUser)
    {
        var balance = _merchantBalances.FirstOrDefault(b =>
            string.Equals(b.Currency, currency, StringComparison.OrdinalIgnoreCase));
        if (balance is not null) return balance;

        balance = MerchantBalance.Create(Id, this, currency, byUser);
        _merchantBalances.Add(balance);

        return balance;
    }

    public void LinkClientAppInfo(ClientAppInfo info, string byUser)
    {
        ClientAppInfo = info;
        SetUpdatedBy(byUser);
    }

    public void LinkLoginInfo(LoginInfo loginInfo, string byUser)
    {
        LoginInfo = loginInfo;
        SetUpdatedBy(byUser);
    }

    public void SetActivationStatus(bool disabled, string byUser)
    {
        Status = disabled ? MerchantStatus.Inactive : MerchantStatus.Active;
        SetUpdatedBy(byUser);
    }

    public void Update(
        string? name,
        string? email,
        string? description,
        string? countryCode,
        string? defaultCurrency,
        string byUser)
    {
        if (!string.IsNullOrEmpty(name)) Name = name;

        if (!string.IsNullOrEmpty(email)) Email = email;

        if (!string.IsNullOrEmpty(description)) Description = description;

        if (!string.IsNullOrEmpty(countryCode)) CountryCode = countryCode;

        if (!string.IsNullOrEmpty(defaultCurrency)) DefaultCurrency = defaultCurrency;

        SetUpdatedBy(byUser);
    }

    public void UpdateFee(MxFee mxFee, string byUser)
    {
        MxFee = mxFee;
        SetUpdatedBy(byUser);
    }

    public void UpdateWebHook(WebHookInfo webHookInfo, string byUser)
    {
        WebHook = webHookInfo;
        SetUpdatedBy(byUser);
    }

    #endregion
}