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
    /// <summary>
    ///     Tracks, per <see cref="IServiceCollection" />, which assemblies <see cref="AddBackgroundJobFrom" /> has
    ///     already scanned so a repeat call with an overlapping assembly set does not register jobs twice.
    /// </summary>
    private sealed class ScannedAssembliesMarker
    {
        public HashSet<Assembly> Assemblies { get; } = [];
    }

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
        {
            services.AddHost();

            if (!services.Any(s => s.ServiceType == typeof(IBackgroundTask) && s.ImplementationType == typeof(TJob)))
                services.AddScoped<IBackgroundTask, TJob>();

            return services;
        }

        /// <summary>
        ///     Scans the specified assemblies and registers all types that implement <see cref="IBackgroundTask" /> as background
        ///     jobs.
        /// </summary>
        /// <param name="assemblies">The assemblies to scan for background job implementations.</param>
        /// <returns>The service collection for method chaining.</returns>
        public IServiceCollection AddBackgroundJobFrom(Assembly[] assemblies)
        {
            services.AddHost();

            var marker = services.FirstOrDefault(s => s.ServiceType == typeof(ScannedAssembliesMarker))
                ?.ImplementationInstance as ScannedAssembliesMarker;

            if (marker is null)
            {
                marker = new ScannedAssembliesMarker();
                services.AddSingleton(marker);
            }

            var newAssemblies = assemblies.Where(a => marker.Assemblies.Add(a)).ToArray();
            if (newAssemblies.Length == 0) return services;

            services.Scan(s =>
                s.FromAssemblies(newAssemblies)
                    .AddClasses(c => c.AssignableTo<IBackgroundTask>(), false)
                    .AsImplementedInterfaces()
                    .WithScopedLifetime());

            return services;
        }

        private IServiceCollection AddHost()
        {
            if (services.Any(s => s.ImplementationType == typeof(BackgroundJobHost)))
                return services;

            services.AddHostedService<BackgroundJobHost>();
            return services;
        }
    }
}