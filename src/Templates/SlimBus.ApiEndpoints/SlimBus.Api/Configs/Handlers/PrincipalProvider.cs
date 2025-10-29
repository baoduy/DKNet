namespace SlimBus.Api.Configs.Handlers;

internal sealed class PrincipalProvider(IHttpContextAccessor accessor) : IPrincipalProvider
{
    #region Fields

    private string _email = null!;
    private Guid _profileId;
    private string _userName = null!;

    #endregion

    #region Properties

    public Guid ProfileId
    {
        get
        {
            this.Initialize();
            return this._profileId;
        }
    }

    public string Email
    {
        get
        {
            this.Initialize();
            return this._email;
        }
    }

    public string UserName
    {
        get
        {
            this.Initialize();
            return this._userName;
        }
    }

    #endregion

    #region Methods

    public ICollection<string> GetAccessibleKeys() => [this.GetOwnershipKey()];

    public string GetOwnershipKey() => this.ProfileId.ToString();

    private void Initialize()
    {
        var context = accessor.HttpContext;
        if (context == null)
        {
            return;
        }

        if (!context.User.Identity?.IsAuthenticated == true || this._profileId != Guid.Empty)
        {
            return;
        }

        this._userName = context.User.Identity?.Name!;

        //Get from ProfileId Claims
        var id = context.User.FindFirst(c =>
            string.Equals(c.Type, ClaimTypes.NameIdentifier, StringComparison.OrdinalIgnoreCase));
        if (id != null && Guid.TryParse(id.Value, out var p))
        {
            this._profileId = p;
        }

        //Get email
        var email = context.User.FindFirst(c =>
            c.Type.Equals("emails", StringComparison.OrdinalIgnoreCase) ||
            c.Type.Equals("email", StringComparison.OrdinalIgnoreCase));
        if (email != null)
        {
            this._email = email.Value;
            if (string.IsNullOrEmpty(this._userName))
            {
                this._userName = this._email;
            }
        }
    }

    #endregion
}