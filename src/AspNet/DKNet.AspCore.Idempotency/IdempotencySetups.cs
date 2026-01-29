// <copyright file="IdempotencySetups.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using DKNet.AspCore.Idempotency.Filters;
using DKNet.AspCore.Idempotency.Stores;
using Microsoft.AspNetCore.Builder;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Extension methods for registering idempotency services with the dependency injection container.
/// </summary>
public static class IdempotencySetups
{
    #region Fields

    private static bool _servicesAdded;

    #endregion

    #region Methods

    /// <summary>
    ///     Adds idempotency services with default configuration to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddIdempotency(this IServiceCollection services)
    {
        return services.AddIdempotency(_ => { });
    }

    /// <summary>
    ///     Adds idempotency services with custom configuration to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An action to configure idempotency options.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddIdempotency(
        this IServiceCollection services,
        Action<IdempotencyOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IIdempotencyKeyRepository, IdempotencyDistributedCacheRepository>();

        _servicesAdded = true;
        return services;
    }

    /// <summary>
    ///     Requires an idempotency key for this endpoint.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The route handler builder for method chaining.</returns>
    /// <remarks>
    ///     The endpoint will return 400 Bad Request if the idempotency key header is missing or invalid.
    ///     Duplicate requests with the same idempotency key will return a cached response.
    /// </remarks>
    public static RouteHandlerBuilder RequireIdempotency(this RouteHandlerBuilder builder)
    {
        if (!_servicesAdded)
            throw new InvalidOperationException(
                "Idempotency services have not been registered. Call AddIdempotency() on the service collection first.");

        builder.AddEndpointFilter<IdempotencyEndpointFilter>();
        return builder;
    }

    /// <summary>
    ///     Requires an idempotency key for this endpoint with custom TTL.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="ttl">The time-to-live for cached responses.</param>
    /// <returns>The route handler builder for method chaining.</returns>
    public static RouteHandlerBuilder RequireIdempotency(this RouteHandlerBuilder builder, TimeSpan ttl)
    {
        return builder.RequireIdempotency(options => options.Expiration = ttl);
    }

    /// <summary>
    ///     Requires an idempotency key for this endpoint with custom configuration.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="configure">An action to configure per-endpoint idempotency options.</param>
    /// <returns>The route handler builder for method chaining.</returns>
    public static RouteHandlerBuilder RequireIdempotency(
        this RouteHandlerBuilder builder,
        Action<IdempotencyOptions> configure)
    {
        if (!_servicesAdded)
            throw new InvalidOperationException(
                "Idempotency services have not been registered. Call AddIdempotency() on the service collection first.");

        // Store the configuration on metadata for the filter to use
        builder.WithMetadata(new IdempotencyEndpointMetadata(configure));
        builder.AddEndpointFilter<IdempotencyEndpointFilter>();
        return builder;
    }

    /// <summary>
    ///     Requires an idempotency key for all endpoints in this route group.
    /// </summary>
    /// <param name="builder">The route group builder.</param>
    /// <returns>The route group builder for method chaining.</returns>
    public static RouteGroupBuilder RequireIdempotency(this RouteGroupBuilder builder)
    {
        if (!_servicesAdded)
            throw new InvalidOperationException(
                "Idempotency services have not been registered. Call AddIdempotency() on the service collection first.");

        builder.AddEndpointFilter<IdempotencyEndpointFilter>();
        return builder;
    }

    #endregion
}

/// <summary>
///     Metadata for per-endpoint idempotency configuration.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class IdempotencyEndpointMetadata
{
    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="IdempotencyEndpointMetadata" /> class.
    /// </summary>
    public IdempotencyEndpointMetadata(Action<IdempotencyOptions> configure) => Configure = configure;

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the configuration action.
    /// </summary>
    public Action<IdempotencyOptions> Configure { get; }

    #endregion
}