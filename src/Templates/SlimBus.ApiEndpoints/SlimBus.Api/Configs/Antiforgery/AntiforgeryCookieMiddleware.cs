namespace SlimBus.Api.Configs.Antiforgery;

internal class AntiforgeryCookieMiddleware(
    RequestDelegate next,
    IAntiforgery antiforgery,
    IOptions<AntiforgeryOptions> options)
{
    #region Fields

    private readonly string[] _validateMethods = ["POST", "PUT", "PATCH", "DELETE"];

    #endregion

    #region Methods

    private AntiforgeryTokenSet GetCookieToken(HttpContext context)
    {
        context.Request.Cookies.TryGetValue(options.Value.HeaderName!, out var requestToken);
        context.Request.Cookies.TryGetValue(options.Value.Cookie.Name!, out var cookieToken);

        return new AntiforgeryTokenSet(
            requestToken,
            cookieToken,
            options.Value.FormFieldName,
            options.Value.HeaderName);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        //Validation
        var method = context.Request.Method;

        AntiforgeryTokenSet token;
        if (this._validateMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
        {
            token = this.GetCookieToken(context);
            context.Request.Headers[options.Value.HeaderName!] = token.RequestToken;
        }
        else
        {
            //Generate Token
            token = antiforgery.GetTokens(context);
            if (string.IsNullOrWhiteSpace(token.CookieToken))
            {
                var oldToken = this.GetCookieToken(context);
                token = new AntiforgeryTokenSet(
                    token.RequestToken,
                    oldToken.CookieToken,
                    token.FormFieldName,
                    token.HeaderName);
            }
        }

        context.Response.Cookies.Append(options.Value.Cookie.Name!, token.CookieToken ?? string.Empty);
        context.Response.Cookies.Append(options.Value.HeaderName!, token.RequestToken ?? string.Empty);

        await next(context);
    }

    #endregion
}