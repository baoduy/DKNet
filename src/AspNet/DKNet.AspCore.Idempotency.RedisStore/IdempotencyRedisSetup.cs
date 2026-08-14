// <copyright file="IdempotencyRedisSetup.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency.RedisStore.Store;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace DKNet.AspCore.Idempotency.RedisStore;

/// <summary>
///     Extension methods for registering Redis-based idempotency storage.
/// </summary>
public static class IdempotencyRedisSetup
{
    #region Methods

    /// <summary>
    ///     Adds Redis-based idempotency key storage to the service collection.
    ///     This registers the StackExchange.Redis cache infrastructure and an <see cref="IConnectionMultiplexer" />
    ///     needed by <see cref="IdempotencyRedisStore" />.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="connectionString">The Redis connection string.</param>
    public static IServiceCollection AddIdempotencyRedisStore(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddStackExchangeRedisCache(options => options.Configuration = connectionString);
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        return services;
    }

    /// <summary>
    ///     Adds Redis-based idempotency key storage to the service collection using an existing connection multiplexer.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="connectionMultiplexer">The Redis connection multiplexer to use.</param>
    public static IServiceCollection AddIdempotencyRedisStore(
        this IServiceCollection services,
        IConnectionMultiplexer connectionMultiplexer)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);

        services.AddSingleton(connectionMultiplexer);
        return services;
    }

    /// <summary>
    ///     Adds Redis-based idempotency key storage and registers <see cref="IdempotencyRedisStore" />
    ///     as the <see cref="IIdempotencyKeyStore" /> implementation.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="connectionString">The Redis connection string.</param>
    /// <param name="config">An optional action to configure <see cref="IdempotencyOptions" />.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    ///     <code>
    ///     builder.Services.AddIdempotencyWithRedisStore(
    ///         builder.Configuration.GetConnectionString("Redis"),
    ///         options =>
    ///         {
    ///             options.Expiration = TimeSpan.FromHours(48);
    ///         });
    ///     </code>
    /// </example>
    public static IServiceCollection AddIdempotencyWithRedisStore(
        this IServiceCollection services,
        string connectionString,
        Action<IdempotencyOptions>? config = null)
    {
        services.AddIdempotencyRedisStore(connectionString);
        return services.AddIdempotentKey<IdempotencyRedisStore>(config);
    }

    #endregion
}
