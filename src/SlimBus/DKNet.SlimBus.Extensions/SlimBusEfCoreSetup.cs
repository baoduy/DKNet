﻿// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: SlimBusEfCoreSetup.cs
// Description: DI helpers to wire SlimMessageBus integrations for EF Core (event publisher and auto-save behavior).

using DKNet.SlimBus.Extensions.Behaviors;
using DKNet.SlimBus.Extensions.Handlers;
using Microsoft.EntityFrameworkCore;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Interceptor;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Service collection extension helpers for wiring SlimMessageBus integrations that work with EF Core.
///     These helpers register the event publisher and an EF Core auto-save post-processor for request handlers.
/// </summary>
public static class SlimBusEfCoreSetup
{
    #region Methods

    /// <summary>
    ///     Registers <see cref="SlimBusEventPublisher" /> as the event publisher for the specified
    ///     <typeparamref name="TDbContext" />.
    ///     The publisher uses SlimMessageBus to publish domain events.
    /// </summary>
    /// <typeparam name="TDbContext">The application's <see cref="DbContext" /> type.</typeparam>
    /// <param name="serviceCollection">The service collection to register services into.</param>
    /// <returns>The same <see cref="IServiceCollection" /> instance for chaining.</returns>
    public static IServiceCollection AddSlimBusEventPublisher<TDbContext>(this IServiceCollection serviceCollection)
        where TDbContext : DbContext
    {
        serviceCollection
            .AddEventPublisher<TDbContext, SlimBusEventPublisher>();
        return serviceCollection;
    }

    /// <summary>
    ///     Adds SlimMessageBus and EF Core integration services required for auto-saving the DbContext after
    ///     request handlers run. Registers the <see cref="EfAutoSavePostProcessor{TRequest,TResponse}" />
    ///     as an <see cref="IRequestHandlerInterceptor{TRequest,TResponse}" /> to be executed by SlimMessageBus.
    /// </summary>
    /// <param name="serviceCollection">The service collection to configure.</param>
    /// <param name="configure">A callback to configure the SlimMessageBus <see cref="MessageBusBuilder" />.</param>
    /// <returns>The same <see cref="IServiceCollection" /> instance for chaining.</returns>
    public static IServiceCollection AddSlimBusForEfCore(
        this IServiceCollection serviceCollection,
        Action<MessageBusBuilder> configure)
    {
        serviceCollection
            .AddScoped(typeof(IRequestHandlerInterceptor<,>), typeof(EfAutoSavePostProcessor<,>))
            .AddSlimMessageBus(configure);
        return serviceCollection;
    }

    #endregion
}