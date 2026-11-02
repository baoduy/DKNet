// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: SlimBusEfCoreSetup.cs
// Description: DI helpers to wire SlimMessageBus integrations for EF Core (event publisher and auto-save behavior).

using DKNet.SlimBus.Extensions.Handlers;
using DKNet.SlimBus.Extensions.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        ///     Adds SlimMessageBus and EF Core integration services required for auto-saving the DbContext after
        ///     request handlers run. Registers the <see cref="EfAutoSavePostInterceptor{TRequest,TResponse}" />
        ///     as an <see cref="IRequestHandlerInterceptor{TRequest,TResponse}" /> to be executed by SlimMessageBus.
        ///     The interceptor is registered once per service collection, however many <typeparamref name="TDbContext" />
        ///     types are added, and regardless of any other <see cref="IRequestHandlerInterceptor{TRequest,TResponse}" />
        ///     registrations made before or after this call.
        /// </summary>
        /// <typeparam name="TDbContext">The <see cref="DbContext" /> type to save after each successful write request.</typeparam>
        /// <returns>The same <see cref="IServiceCollection" /> instance for chaining.</returns>
        public IServiceCollection AddSlimBusEfCoreInterceptor<TDbContext>()
            where TDbContext : DbContext
        {
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IAutoSaveDbContextRegistration, AutoSaveDbContextRegistration<TDbContext>>());
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Scoped(typeof(IRequestHandlerInterceptor<,>), typeof(EfAutoSavePostInterceptor<,>)));
            return serviceCollection;
        }
    }
}