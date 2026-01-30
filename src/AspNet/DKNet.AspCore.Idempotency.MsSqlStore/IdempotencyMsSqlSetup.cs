// <copyright file="ServiceCollectionExtensions.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency.MsSqlStore.Data;
using DKNet.AspCore.Idempotency.MsSqlStore.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.AspCore.Idempotency.MsSqlStore;

/// <summary>
///     Extension methods for registering MS SQL Server-based idempotency storage.
/// </summary>
public static class IdempotencyMsSqlSetup
{
    #region Methods

    /// <summary>
    ///     Adds MS SQL Server-based idempotency key storage to the service collection.
    ///     This registers the DbContext and replaces the default cache-based store with SQL Server storage.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="connectionString">The SQL Server connection string.</param>
    public static IServiceCollection AddIdempotencyMsSqlStore(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Register DbContext
        services.AddDbContext<IdempotencyDbContext>(options =>
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions
                        .MigrationsAssembly(typeof(IdempotencyMsSqlSetup).Assembly)
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
    ///     Adds MS SQL Server-based idempotency key storage to the service collection.
    ///     This registers the DbContext and replaces the default cache-based store with SQL Server storage.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <param name="config"></param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    ///     <code>
    ///     builder.Services.AddIdempotencyMsSqlStore(
    ///         builder.Configuration.GetConnectionString("IdempotencyDb"),
    ///         options =>
    ///         {
    ///             options.Expiration = TimeSpan.FromHours(48);
    ///             options.FailOpen = false;
    ///         });
    ///     </code>
    /// </example>
    public static IServiceCollection AddIdempotencyWithMsSqlStore(
        this IServiceCollection services,
        string connectionString,
        Action<IdempotencyOptions>? config = null)
    {
        // Register DbContext
        services.AddIdempotencyMsSqlStore(connectionString);
        return services.AddIdempotentKey<IdempotencySqlServerStore>(config);
    }

    #endregion
}