namespace SlimBus.Api.Configs.Handlers;

internal class SetUserIdPropertyFilter(IOptions<FeatureOptions> options) : IEndpointFilter
{
    #region Fields

    private readonly FeatureOptions _options = options.Value;

    #endregion

    #region Methods

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var userName = string.Empty;

        if (this._options.RequireAuthorization)
        {
            if (context.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                userName = context.HttpContext.User.Identity.Name!;
            }
        }
        else
        {
            userName = SharedConsts.SystemAccount;
        }

        foreach (var a in context.Arguments)
        {
            if (a is not BaseCommand b)
            {
                continue;
            }

            b.ByUser = userName;
            Console.WriteLine("Set user for model: " + b.GetType().Name);
        }

        return await next(context);
    }

    #endregion
}