// <copyright file="IdempotencyNpgsqlSetup.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency.NpgsqlStore.Data;
using DKNet.AspCore.Idempotency.NpgsqlStore.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.AspCore.Idempotency.NpgsqlStore;

/// <summary>
///     Extension methods for registering PostgreSQL-based idempotency storage.
/// </summary>
public static class IdempotencyNpgsqlSetup
{
    #region Methods

    /// <summary>
    ///     Adds PostgreSQL-based idempotency key storage to the service collection.
    ///     This registers the DbContext and replaces the default cache-based store with PostgreSQL storage.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    public static IServiceCollection AddIdempotencyNpgsqlStore(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Register DbContext
        services.AddDbContext<IdempotencyDbContext>(options =>
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions
                        .MigrationsAssembly(typeof(IdempotencyNpgsqlSetup).Assembly)
                        .MigrationsHistoryTable(nameof(IdempotencyDbContext), "migrate")
                        .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                        .EnableRetryOnFailure(3,
                            TimeSpan.FromSeconds(5),
                            null);
                });
            }, optionsLifetime: ServiceLifetime.Singleton)
            .AddDbContextFactory<IdempotencyDbContext>();
        return services;
    }

    /// <summary>
    ///     Adds PostgreSQL-based idempotency key storage to the service collection.
    ///     This registers the DbContext and replaces the default cache-based store with PostgreSQL storage.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <param name="config"></param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    ///     <code>
    ///     builder.Services.AddIdempotencyWithNpgsqlStore(
    ///         builder.Configuration.GetConnectionString("IdempotencyDb"),
    ///         options =>
    ///         {
    ///             options.Expiration = TimeSpan.FromHours(48);
    ///             options.FailOpen = false;
    ///         });
    ///     </code>
    /// </example>
    public static IServiceCollection AddIdempotencyWithNpgsqlStore(
        this IServiceCollection services,
        string connectionString,
        Action<IdempotencyOptions>? config = null)
    {
        // Register DbContext
        services.AddIdempotencyNpgsqlStore(connectionString);
        return services.AddIdempotentKey<IdempotencyPostgresStore>(config);
    }

    #endregion
}
