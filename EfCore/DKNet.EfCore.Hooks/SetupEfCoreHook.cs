﻿using System.Diagnostics;
using DKNet.EfCore.Hooks.Internals;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.Hooks;

public static class SetupEfCoreHook
{
    /// <summary>
    /// Add HookRunner from ServiceProvider to DbContext Interceptors.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    public static DbContextOptionsBuilder AddHookInterceptor<TDbContext>(
        this DbContextOptionsBuilder options,
        IServiceProvider provider) where TDbContext : DbContext =>
        options.AddInterceptors(provider.GetRequiredKeyedService<HookRunner>(typeof(TDbContext).FullName));

    /// <summary>
    ///     Add Implementation of <see /> or <see cref="IHookAsync" /> to <see cref="IServiceCollection" />
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddHook<TDbContext, THook>(this IServiceCollection services)
        where TDbContext : DbContext where THook : class, IHookBaseAsync
    {
        var key = typeof(TDbContext).FullName!;
        var type = typeof(THook);

        if (services.Any(s =>
                s.IsKeyedService && ReferenceEquals(s.ServiceKey, key) &&
                s.KeyedImplementationType == type))
        {
            Trace.WriteLine($"The Hook {type.Name} already added.");
            return services;
        }

        //Ensure HookRunner is Added
        services.AddHookRunner<TDbContext>();

        return services
            .AddKeyedScoped<THook>(key)
            .AddKeyedScoped<IHookBaseAsync>(key, (p, k) => p.GetRequiredKeyedService<THook>(k));
    }

    /// <summary>
    ///     Al registered IHooks need this runner to run. If not provided, they will be ignored.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    internal static IServiceCollection AddHookRunner<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        var fullName = typeof(TDbContext).FullName;

        if (services.Any(s =>
                s.IsKeyedService && ReferenceEquals(s.ServiceKey, fullName) &&
                s.KeyedImplementationType == typeof(HookRunner)))
        {
            Trace.WriteLine($"The {nameof(HookRunner)} already registered.");
            return services;
        }

        return services
            .AddScoped<HookFactory>()
            .AddKeyedScoped<HookRunner>(fullName);
    }

    /// <summary>
    ///     Add Hook Runner to <see cref="IServiceCollection" /> and register the Hook Interceptor to
    /// </summary>
    ///     <see cref="DbContextOptionsBuilder" />
    ///     <param name="services"></param>
    ///     <param name="builder"></param>
    ///     <param name="contextLifetime"></param>
    ///     <param name="optionLifetime"></param>
    ///     <returns></returns>
    /// 
    public static IServiceCollection AddDbContextWithHook<TDbContext>(this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> builder,
        ServiceLifetime contextLifetime = ServiceLifetime.Scoped,
        ServiceLifetime optionLifetime = ServiceLifetime.Scoped) where TDbContext : DbContext
    {
        services
            .AddHookRunner<TDbContext>()
            .AddDbContext<TDbContext>((provider, options) =>
            {
                builder(provider, options);
                options.AddHookInterceptor<TDbContext>(provider);
            }, contextLifetime, optionLifetime);

        return services;
    }

    /// <summary>
    ///     Add Hook Runner to <see cref="IServiceCollection" /> and register the Hook Interceptor to
    /// </summary>
    ///     <see cref="DbContextOptionsBuilder" />
    ///     <param name="services"></param>
    ///     <param name="builder"></param>
    ///     <param name="contextLifetime"></param>
    ///     <param name="optionLifetime"></param>
    ///     <returns></returns>
    ///
    public static IServiceCollection AddDbContextWithHook<TDbContext>(this IServiceCollection services,
        Action<DbContextOptionsBuilder> builder,
        ServiceLifetime contextLifetime = ServiceLifetime.Scoped,
        ServiceLifetime optionLifetime = ServiceLifetime.Scoped) where TDbContext : DbContext
    {
        services
            .AddHookRunner<TDbContext>()
            .AddDbContext<TDbContext>((provider, options) =>
            {
                builder(options);
                options.AddHookInterceptor<TDbContext>(provider);
            }, contextLifetime, optionLifetime);

        return services;
    }
}