using System.Reflection;
using DKNet.AspNetCore.BackgroundJobs;
using DKNet.AspNetCore.BackgroundJobs.Internals;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class BackgroundJobSetups
{
    private static bool _added;

    private static IServiceCollection AddHost(this IServiceCollection services)
    {
        if (_added) return services;
        services.AddHostedService<BackgroundJobHost>();
        _added = true;
        return services;
    }

    public static IServiceCollection AddBackgroundJob<TJob>(this IServiceCollection services)
        where TJob : class, IBackgroundJob
        => services.AddHost().AddScoped<IBackgroundJob, TJob>();

    public static IServiceCollection AddBackgroundJobFrom(this IServiceCollection services, Assembly[] assemblies)
    {
        services.AddHost().Scan(s =>
            s.FromAssemblies(assemblies)
                .AddClasses(c => c.AssignableTo<IBackgroundJob>(), false)
                .AsImplementedInterfaces()
                .WithScopedLifetime());
        return services;
    }
}