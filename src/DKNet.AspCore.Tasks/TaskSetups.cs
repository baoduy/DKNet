// <copyright file="TaskSetups.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Reflection;
using DKNet.AspCore.Tasks;
using DKNet.AspCore.Tasks.Internals;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Provides extension methods for registering background tasks with the dependency injection container.
/// </summary>
public static class TaskSetups
{
    #region Fields

    private static bool _added;

    #endregion

    /// <param name="services">The service collection to add the background job to.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        ///     Registers a background job of type <typeparamref name="TJob" /> to run during application startup.
        /// </summary>
        /// <typeparam name="TJob">The type of the background job to register. Must implement <see cref="IBackgroundTask" />.</typeparam>
        /// <returns>The service collection for method chaining.</returns>
        public IServiceCollection AddBackgroundJob<TJob>()
            where TJob : class, IBackgroundTask
            => services.AddHost().AddScoped<IBackgroundTask, TJob>();

        /// <summary>
        ///     Scans the specified assemblies and registers all types that implement <see cref="IBackgroundTask" /> as background
        ///     jobs.
        /// </summary>
        /// <param name="assemblies">The assemblies to scan for background job implementations.</param>
        /// <returns>The service collection for method chaining.</returns>
        public IServiceCollection AddBackgroundJobFrom(Assembly[] assemblies)
        {
            services.AddHost().Scan(s =>
                s.FromAssemblies(assemblies)
                    .AddClasses(c => c.AssignableTo<IBackgroundTask>(), false)
                    .AsImplementedInterfaces()
                    .WithScopedLifetime());
            return services;
        }

        private IServiceCollection AddHost()
        {
            if (_added) return services;

            services.AddHostedService<BackgroundJobHost>();
            _added = true;
            return services;
        }
    }
}