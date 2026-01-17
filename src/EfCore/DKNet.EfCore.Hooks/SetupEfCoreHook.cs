using System.Diagnostics;
using DKNet.EfCore.Hooks.Internals;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.Hooks;

/// <summary>
///     The setup for EF Core Hooks
/// </summary>
public static class SetupEfCoreHook
{
    #region Methods

    /// <summary>
    ///     Disable Hooks for the current DbContext scope. It useful when running data migration and Data seeding.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static IHookDisablingContext DisableHooks(this DbContext context) =>
        new HookDisablingContext(context);

    /// <summary>
    ///     Add HookRunner from ServiceProvider to DbContext Interceptors.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    public static DbContextOptionsBuilder UseHooks<TDbContext>(
        this DbContextOptionsBuilder options,
        IServiceProvider provider) where TDbContext : DbContext =>
        options.AddInterceptors(provider.GetRequiredKeyedService<HookRunnerInterceptor>(typeof(TDbContext).FullName));

    #endregion

    /// <param name="services"></param>
    extension(IServiceCollection services)
    {
        /// <summary>
        ///     Add Hook Runner to <see cref="IServiceCollection" /> and register the Hook Interceptor to
        /// </summary>
        /// <see cref="DbContextOptionsBuilder" />
        /// <param name="builder"></param>
        /// <param name="contextLifetime"></param>
        /// <param name="optionLifetime"></param>
        /// <returns></returns>
        public IServiceCollection AddDbContextWithHook<TDbContext>(
            Action<IServiceProvider, DbContextOptionsBuilder> builder,
            ServiceLifetime contextLifetime = ServiceLifetime.Scoped,
            ServiceLifetime optionLifetime = ServiceLifetime.Singleton) where TDbContext : DbContext
        {
            services
                .AddHookRunner<TDbContext>()
                .AddDbContext<TDbContext>(
                    (provider, options) =>
                    {
                        builder(provider, options);
                        options.UseHooks<TDbContext>(provider);
                    },
                    contextLifetime,
                    optionLifetime);

            return services;
        }

        /// <summary>
        ///     Add Hook Runner to <see cref="IServiceCollection" /> and register the Hook Interceptor to
        /// </summary>
        /// <see cref="DbContextOptionsBuilder" />
        /// <param name="builder"></param>
        /// <param name="contextLifetime"></param>
        /// <param name="optionLifetime"></param>
        /// <returns></returns>
        public IServiceCollection AddDbContextWithHook<TDbContext>(Action<DbContextOptionsBuilder<TDbContext>> builder,
            ServiceLifetime contextLifetime = ServiceLifetime.Scoped,
            ServiceLifetime optionLifetime = ServiceLifetime.Singleton) where TDbContext : DbContext
        {
            services
                .AddHookRunner<TDbContext>()
                .AddDbContext<TDbContext>(
                    (provider, options) =>
                    {
                        builder((DbContextOptionsBuilder<TDbContext>)options);
                        options.UseHooks<TDbContext>(provider);
                    },
                    contextLifetime,
                    optionLifetime);

            return services;
        }

        /// <summary>
        ///     Add Implementation of <see /> or <see cref="IHookAsync" /> to <see cref="IServiceCollection" />
        /// </summary>
        /// <returns></returns>
        public IServiceCollection AddHook<TDbContext, THook>()
            where TDbContext : DbContext where THook : class, IHookBaseAsync
        {
            var key = typeof(TDbContext).FullName!;
            var type = typeof(THook);

            if (services.Any(s => s.IsKeyedImplementationOf<THook>(key)))
            {
                Debug.WriteLine($"The Hook {type.Name} already added.");
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
        /// <returns></returns>
        internal IServiceCollection AddHookRunner<TDbContext>()
            where TDbContext : DbContext
        {
            var fullName = typeof(TDbContext).FullName!;

            if (services.Any(s => s.IsKeyedImplementationOf<HookRunnerInterceptor>(fullName)))
            {
                Debug.WriteLine($"The {nameof(HookRunnerInterceptor)} already registered.");
                return services;
            }

            return services
                .AddScoped<HookFactory>()
                .AddKeyedScoped<HookRunnerInterceptor>(fullName);
        }
    }
}