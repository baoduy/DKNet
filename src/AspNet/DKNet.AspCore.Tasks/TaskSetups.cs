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

            var jobTypes = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IBackgroundTask).IsAssignableFrom(t));

            foreach (var jobType in jobTypes)
            {
                if (!services.Any(s => s.ServiceType == typeof(IBackgroundTask) && s.ImplementationType == jobType))
                    services.AddScoped(typeof(IBackgroundTask), jobType);
            }

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