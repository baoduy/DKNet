using System.Reflection;
using DKNet.AspCore.Tasks;
using DKNet.AspCore.Tasks.Internals;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class TaskSetups
{
    #region Fields

    private static bool _added;

    #endregion

    #region Methods

    public static IServiceCollection AddBackgroundJob<TJob>(this IServiceCollection services)
        where TJob : class, IBackgroundTask
        => services.AddHost().AddScoped<IBackgroundTask, TJob>();

    public static IServiceCollection AddBackgroundJobFrom(this IServiceCollection services, Assembly[] assemblies)
    {
        services.AddHost().Scan(s =>
            s.FromAssemblies(assemblies)
                .AddClasses(c => c.AssignableTo<IBackgroundTask>(), false)
                .AsImplementedInterfaces()
                .WithScopedLifetime());
        return services;
    }

    private static IServiceCollection AddHost(this IServiceCollection services)
    {
        if (_added) return services;
        services.AddHostedService<BackgroundJobHost>();
        _added = true;
        return services;
    }

    #endregion
}