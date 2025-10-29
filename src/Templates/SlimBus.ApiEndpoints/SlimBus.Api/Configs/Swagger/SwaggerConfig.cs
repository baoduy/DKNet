using Microsoft.OpenApi.Models;

namespace SlimBus.Api.Configs.Swagger;

[SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters")]
[ExcludeFromCodeCoverage]
internal static class SwaggerConfig
{
    #region Fields

    private static bool _configAdded;

    #endregion

    #region Methods

    public static IServiceCollection AddOpenApiDoc(this IServiceCollection services, FeatureOptions features)
    {
        services.AddOpenApiDocVersion(1, features.RequireAuthorization)
            .AddOpenApiDocVersion(2, features.RequireAuthorization);

        _configAdded = true;
        return services;
    }

    private static IServiceCollection AddOpenApiDocVersion(
        this IServiceCollection services,
        int version,
        bool enableAuthentication)
    {
        var docName = $"v{version}";
        return services.AddOpenApi(
            docName,
            c =>
            {
                c.ShouldInclude = description =>
                    description.GroupName == null || string.Equals(
                        description.GroupName,
                        docName,
                        StringComparison.OrdinalIgnoreCase);

                if (enableAuthentication)
                {
                    c.AddDocumentTransformer<BearerSecurityTransformer>();
                }

                c.AddDocumentTransformer((doc, _, _) =>
                {
                    doc.Info.Title = $"{SharedConsts.ApiName} API Version {version}";
                    doc.Servers.Add(
                        new OpenApiServer
                        {
                            Description = "LocalHost",
                            Url = "http://localhost:5000"
                        });

                    var paths = new OpenApiPaths();
                    foreach (var openApiPath in doc.Paths)
                    {
                        var key = openApiPath.Key.Replace(
                            "v{version}",
                            $"v{version}",
                            StringComparison.OrdinalIgnoreCase);
                        paths.Add(key, openApiPath.Value);
                    }

                    doc.Paths = paths;
                    return Task.CompletedTask;
                });
            });
    }

    public static WebApplication UseOpenApiDoc(this WebApplication app)
    {
        if (!_configAdded)
        {
            return app;
        }

        app.MapOpenApi();
        app.MapScalarApiReference(
            "/docs",
            c =>
                c.WithTitle($"{SharedConsts.ApiName} API")
                    .WithTheme(ScalarTheme.Default)

                    //.WithOpenApiRoutePattern("{documentName}.json")
                    .AddPreferredSecuritySchemes("Bearer")
                    .AddHttpAuthentication("Bearer", b => b.Token = "bearer token"));

        Console.WriteLine("Swagger enabled.");
        return app;
    }

    #endregion
}