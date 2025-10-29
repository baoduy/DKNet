namespace SlimBus.Api.Configs.Antiforgery;

[ExcludeFromCodeCoverage]
internal static class AntiforgeryConfig
{
    #region Fields

    private static bool _configAdded;

    #endregion

    #region Methods

    public static IServiceCollection AddAntiforgeryConfig(
        this IServiceCollection services,
        string cookieName = "x-csrf-cookie",
        string headerName = "x-csrf-header",
        string formFieldName = "x-csrf-field")
    {
        services.AddAntiforgery(config =>
        {
            config.Cookie.Name = cookieName;
            config.HeaderName = headerName;
            config.FormFieldName = formFieldName;

            config.Cookie.HttpOnly = true;
            config.Cookie.SameSite = SameSiteMode.Strict;
            config.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            config.SuppressXFrameOptionsHeader = false;
        });
        _configAdded = true;
        return services;
    }

    public static WebApplication UseAntiforgeryConfig(this WebApplication app)
    {
        if (_configAdded)
        {
            app.UseMiddleware<AntiforgeryCookieMiddleware>();
            app.UseCookiePolicy();
            Console.WriteLine("Antiforgery enabled.");
        }

        return app;
    }

    #endregion
}