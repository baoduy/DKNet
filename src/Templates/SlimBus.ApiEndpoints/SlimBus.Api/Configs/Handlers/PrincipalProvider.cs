namespace SlimBus.Api.Configs.Handlers;

internal sealed class PrincipalProvider(IHttpContextAccessor accessor) : IPrincipalProvider
{
    private string _email = null!;
    private Guid _profileId;
    private string _userName = null!;

    public string Email
    {
        get
        {
            Initialize();
            return _email;
        }
    }

    public Guid ProfileId
    {
        get
        {
            Initialize();
            return _profileId;
        }
    }

    public string UserName
    {
        get
        {
            Initialize();
            return _userName;
        }
    }


    public ICollection<string> GetAccessibleKeys() => [GetOwnershipKey()];


    public string GetOwnershipKey() => ProfileId.ToString();

    private void Initialize()
    {
        var context = accessor.HttpContext;
        if (context == null) return;

        if (!context.User.Identity?.IsAuthenticated == true || _profileId != Guid.Empty) return;

        _userName = context.User.Identity?.Name!;

        //Get from ProfileId Claims
        var id = context.User.FindFirst(c =>
            string.Equals(c.Type, ClaimTypes.NameIdentifier, StringComparison.OrdinalIgnoreCase));
        if (id != null && Guid.TryParse(id.Value, out var p))
            _profileId = p;

        //Get email
        var email = context.User.FindFirst(c =>
            c.Type.Equals("emails", StringComparison.OrdinalIgnoreCase) ||
            c.Type.Equals("email", StringComparison.OrdinalIgnoreCase));
        if (email != null)
        {
            _email = email.Value;
            if (string.IsNullOrEmpty(_userName))
                _userName = _email;
        }
    }
}