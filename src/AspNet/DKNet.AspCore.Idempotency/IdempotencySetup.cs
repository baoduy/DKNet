using System.Diagnostics.CodeAnalysis;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DKNet.AspCore.Idempotency;

/// <summary>
///     Provides extension methods for setting up idempotency support in ASP.NET Core applications.
///     This static class handles dependency injection registration and endpoint filter configuration
///     for idempotent request processing using distributed caching.
/// </summary>
[ExcludeFromCodeCoverage]
public static class IdempotencySetup
{
    #region Methods

    /// <summary>
    ///     Registers idempotency services into the dependency injection container.
    ///     This includes the <see cref="IIdempotencyKeyStore" /> implementation and configuration options.
    /// </summary>
    /// <param name="services">The service collection to register idempotency services into.</param>
    /// <param name="config">
    ///     An optional action to configure <see cref="IdempotencyOptions" />.
    ///     If null, default options are used.
    /// </param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    ///     This method must be called before adding endpoint filters that use <see cref="RequiredIdempotentKey" />.
    ///     It registers:
    ///     - <typeparamref name="TSoreImplement" /> as the <see cref="IIdempotencyKeyStore" /> implementation
    ///     - Configuration options as a singleton
    ///     <para>
    ///         For concurrency-safe storage prefer an atomic store implementation such as
    ///         <c>IdempotencySqlServerStore</c>, <c>IdempotencyPostgresStore</c>, or <c>IdempotencyRedisStore</c>.
    ///         The distributed-cache store is convenient for single-instance or development scenarios but does not
    ///         eliminate the check-then-act race window.
    ///     </para>
    /// </remarks>
    public static IServiceCollection AddIdempotentKey<TSoreImplement>(this IServiceCollection services,
        Action<IdempotencyOptions>? config = null) where TSoreImplement : class, IIdempotencyKeyStore
    {
        if (services.IsRegistered<IIdempotencyKeyStore>())
            return services;

        var options = new IdempotencyOptions();
        config?.Invoke(options);

        // Validate configuration
        ValidateOptions(options);

        services
            .AddSingleton<IIdempotencyKeyStore, TSoreImplement>()
            .AddSingleton(Options.Create(options));

        return services;
    }

    /// <summary>
    ///     Registers idempotency services into the dependency injection container.
    ///     This includes the <see cref="IIdempotencyKeyStore" /> implementation and configuration options.
    /// </summary>
    /// <param name="services">The service collection to register idempotency services into.</param>
    /// <param name="config">
    ///     An optional action to configure <see cref="IdempotencyOptions" />.
    ///     If null, default options are used.
    /// </param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    ///     This method must be called before adding endpoint filters that use <see cref="RequiredIdempotentKey" />.
    ///     It registers:
    ///     - <see cref="IdempotencyDistributedCacheStore" /> as the <see cref="IIdempotencyKeyStore" />
    ///     implementation
    ///     - Configuration options as a singleton
    ///     <para>
    ///         The default <see cref="IdempotencyDistributedCacheStore" /> narrows but does not eliminate the
    ///         check-then-act race window because <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache" /> does not provide an atomic
    ///         compare-and-set primitive. For concurrent environments use an atomic store such as
    ///         <c>AddIdempotentKey&lt;IdempotencySqlServerStore&gt;()</c>,
    ///         <c>AddIdempotentKey&lt;IdempotencyPostgresStore&gt;()</c>, or
    ///         <c>AddIdempotentKey&lt;IdempotencyRedisStore&gt;()</c>.
    ///     </para>
    /// </remarks>
    [Obsolete("The default IdempotencyDistributedCacheStore is not atomic under concurrency. " +
              "Use AddIdempotentKey<IdempotencySqlServerStore>(), AddIdempotentKey<IdempotencyPostgresStore>(), " +
              "or AddIdempotentKey<IdempotencyRedisStore>() instead.")]
    public static IServiceCollection AddIdempotentKey(this IServiceCollection services,
        Action<IdempotencyOptions>? config = null) =>
        services.AddIdempotentKey<IdempotencyDistributedCacheStore>(config);

    /// <summary>
    ///     Adds the idempotency endpoint filter to a route handler.
    ///     This filter validates the presence of an idempotency key header and prevents duplicate request processing.
    /// </summary>
    /// <param name="builder">The route handler builder to add the filter to.</param>
    /// <returns>The route handler builder for method chaining.</returns>
    /// <remarks>
    ///     The <see cref="AddIdempotentKey" /> method must be called during service registration
    ///     before this method will have any effect. If idempotency has not been configured,
    ///     the filter is not added and a warning should be logged.
    ///     Typical usage:
    ///     <code>
    ///     app.MapPost("/orders", CreateOrder)
    ///         .RequiredIdempotentKey();
    ///     </code>
    /// </remarks>
    public static RouteHandlerBuilder RequiredIdempotentKey(this RouteHandlerBuilder builder)
    {
        builder.AddEndpointFilter<IdempotencyEndpointFilter>();
        return builder;
    }

    /// <summary>
    ///     Validates the idempotency configuration options.
    /// </summary>
    /// <param name="options">The options to validate.</param>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    private static void ValidateOptions(IdempotencyOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.IdempotencyHeaderKey))
            throw new ArgumentException(
                "IdempotencyHeaderKey cannot be empty or whitespace.",
                nameof(options.IdempotencyHeaderKey));

        if (string.IsNullOrWhiteSpace(options.CachePrefix))
            throw new ArgumentException(
                "CachePrefix cannot be empty or whitespace.",
                nameof(options.CachePrefix));

        if (options.ScopeHmacSecret is not null && string.IsNullOrWhiteSpace(options.ScopeHmacSecret))
            throw new ArgumentException(
                "ScopeHmacSecret cannot be empty or whitespace.",
                nameof(options.ScopeHmacSecret));

        if (options.Expiration <= TimeSpan.Zero)
            throw new ArgumentException(
                $"Expiration must be positive. Current value: {options.Expiration}",
                nameof(options.Expiration));

        if (options.JsonSerializerOptions is null)
            throw new ArgumentException(
                "JsonSerializerOptions cannot be null.",
                nameof(options.JsonSerializerOptions));

        if (options.MaxIdempotencyKeyLength < 1)
            throw new ArgumentException(
                "MaxIdempotencyKeyLength must be at least 1.",
                nameof(options.MaxIdempotencyKeyLength));

        if (string.IsNullOrWhiteSpace(options.IdempotencyKeyPattern))
            throw new ArgumentException(
                "IdempotencyKeyPattern cannot be empty.",
                nameof(options.IdempotencyKeyPattern));

        if (options.MinStatusCodeForCaching < 100)
            throw new ArgumentException(
                "MinStatusCodeForCaching must be >= 100.",
                nameof(options.MinStatusCodeForCaching));

        if (options.MaxStatusCodeForCaching > 599)
            throw new ArgumentException(
                "MaxStatusCodeForCaching must be <= 599.",
                nameof(options.MaxStatusCodeForCaching));

        if (options.MinStatusCodeForCaching > options.MaxStatusCodeForCaching)
            throw new ArgumentException(
                $"MinStatusCodeForCaching ({options.MinStatusCodeForCaching}) cannot be greater than " +
                $"MaxStatusCodeForCaching ({options.MaxStatusCodeForCaching}).",
                nameof(options.MinStatusCodeForCaching));
    }

    #endregion
}