// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: SlimBusEfCoreSetup.cs
// Description: DI helpers to wire SlimMessageBus integrations for EF Core (event publisher and auto-save behavior).

using DKNet.SlimBus.Extensions.Handlers;
using DKNet.SlimBus.Extensions.Interceptors;
using Microsoft.EntityFrameworkCore;
using SlimMessageBus.Host.Interceptor;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Service collection extension helpers for wiring SlimMessageBus integrations that work with EF Core.
///     These helpers register the event publisher and an EF Core auto-save post-processor for request handlers.
/// </summary>
public static class SlimBusEfCoreSetup
{
    /// <param name="serviceCollection">The service collection to register services into.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        ///     Registers <see cref="SlimBusEventPublisher" /> as the event publisher for the specified
        ///     <typeparamref name="TDbContext" />.
        ///     The publisher uses SlimMessageBus to publish domain events.
        /// </summary>
        /// <typeparam name="TDbContext">The application's <see cref="DbContext" /> type.</typeparam>
        /// <returns>The same <see cref="IServiceCollection" /> instance for chaining.</returns>
        public IServiceCollection AddSlimBusEventPublisher<TDbContext>()
            where TDbContext : DbContext
        {
            serviceCollection
                .AddEventPublisher<TDbContext, SlimBusEventPublisher>();
            return serviceCollection;
        }

        /// <summary>
        ///     Registers a custom EF Core exception handler for the specified <typeparamref name="TDbContext" />.
        /// </summary>
        /// <typeparam name="TDbContext"></typeparam>
        /// <typeparam name="TExceptionHandler"></typeparam>
        /// <returns></returns>
        public IServiceCollection AddSlimBusEfCoreExceptionHandler<TDbContext, TExceptionHandler>()
            where TDbContext : DbContext
            where TExceptionHandler : class, IEfCoreExceptionHandler =>
            serviceCollection.AddKeyedTransient<IEfCoreExceptionHandler, TExceptionHandler>(typeof(TDbContext)
                .FullName);

        /// <summary>
        ///     Adds SlimMessageBus and EF Core integration services required for auto-saving the DbContext after
        ///     request handlers run. Registers the <see cref="EfAutoSavePostInterceptor{TRequest,TResponse}" />
        ///     as an <see cref="IRequestHandlerInterceptor{TRequest,TResponse}" /> to be executed by SlimMessageBus.
        /// </summary>
        /// <returns>The same <see cref="IServiceCollection" /> instance for chaining.</returns>
        public IServiceCollection AddSlimBusEfCoreInterceptor<TDbContext>()
            where TDbContext : DbContext
        {
            EfAutoSavePostProcessorRegistration.RegisterDbContextType<TDbContext>();

            if (serviceCollection.Any(serviceDescriptor =>
                    serviceDescriptor.ServiceType == typeof(IRequestHandlerInterceptor<,>)))
                return serviceCollection;

            serviceCollection
                .AddScoped(typeof(IRequestHandlerInterceptor<,>), typeof(EfAutoSavePostInterceptor<,>));
            return serviceCollection;
        }
    }
}