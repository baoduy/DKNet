using Asp.Versioning.Conventions;
using DKNet.Fw.Extensions.TypeExtractors;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;
using SlimBus.Api.Configs;
using SlimBus.Api.Configs.Auth;

// ReSharper disable once CheckNamespace
namespace Microsoft.AspNetCore.Builder;

[ExcludeFromCodeCoverage]
internal static class EndpointConfig
{
    private static ApiVersionSet CreateVersionSet(this WebApplication app, IEnumerable<int> versions)
    {
        return app.NewApiVersionSet()
            .HasApiVersions(versions.Distinct().Select(v => new ApiVersion(v)))
            .ReportApiVersions()
            .Build();
    }

    private static RouteGroupBuilder CreateGroup(this WebApplication app, ApiVersionSet versionSet,
        IEndpointConfig config)
    {
        var path = $"/v{{version:apiVersion}}{config.GroupEndpoint}";
        var displayName = $"/v{config.Version}{config.GroupEndpoint}";

        var group = app.MapGroup(path)
            .AddEndpointFilter<SetUserIdPropertyFilter>()
            .WithApiVersionSet(versionSet)
            .HasApiVersion(config.Version)
            .MapToApiVersion(config.Version)
            //Swagger config
            .WithDisplayName(displayName)
            .WithGroupName($"v{config.Version}")
            .WithTags(config.GroupEndpoint.Replace("/", string.Empty, StringComparison.OrdinalIgnoreCase));

        if (FluentValidationConfig.ConfigAdded)
            group.AddFluentValidationAutoValidation();

        if (AuthConfig.IsAuthConfigAdded)
            group.RequireAuthorization();

        return group;
    }

    public static List<RouteGroupBuilder> UseEndpointConfigs(this WebApplication app)
    {
        var groupList = new List<RouteGroupBuilder>();
        var configs = typeof(EndpointConfig).Assembly.Extract().Classes().IsAssignableTo<IEndpointConfig>();
        var configInstances = configs.Select(c => (IEndpointConfig)Activator.CreateInstance(c)!).ToList();
        var vSets = app.CreateVersionSet(configInstances.Select(c => c.Version));

        foreach (var instance in configInstances)
        {
            var group = app.CreateGroup(vSets, instance);
            instance.Map(group);
            groupList.Add(group);
        }

        return groupList;
    }
}