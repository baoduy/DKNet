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
///     for idempotent request processing.
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
    ///     - <see cref="IdempotencyOptions" /> via the options pattern, validated with <c>ValidateOnStart()</c>
    ///     so a misconfiguration still fails fast at application startup rather than on first resolve.
    ///     <para>
    ///         For concurrency-safe storage prefer an atomic store implementation such as
    ///         <c>IdempotencySqlServerStore</c>, <c>IdempotencyPostgresStore</c>, or <c>IdempotencyRedisStore</c>.
    ///         The distributed-cache store is convenient for single-instance or development scenarios but does not
    ///         eliminate the check-then-act race window.
    ///     </para>
    ///     <para>
    ///         A second call is a complete no-op (including its <paramref name="config" />) once an
    ///         <see cref="IIdempotencyKeyStore" /> is already registered - the first caller always wins.
    ///     </para>
    /// </remarks>
    public static IServiceCollection AddIdempotentKey<TSoreImplement>(this IServiceCollection services,
        Action<IdempotencyOptions>? config = null) where TSoreImplement : class, IIdempotencyKeyStore
    {
        if (services.IsRegistered<IIdempotencyKeyStore>())
            return services;

        services.AddSingleton<IIdempotencyKeyStore, TSoreImplement>();

        var optionsBuilder = services.AddOptions<IdempotencyOptions>();
        if (config is not null)
            optionsBuilder.Configure(config);

        optionsBuilder
            .Validate(o => !string.IsNullOrWhiteSpace(o.IdempotencyHeaderKey),
                "IdempotencyHeaderKey cannot be empty or whitespace.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.CachePrefix), "CachePrefix cannot be empty or whitespace.")
            .Validate(o => o.ScopeHmacSecret is null || !string.IsNullOrWhiteSpace(o.ScopeHmacSecret),
                "ScopeHmacSecret cannot be empty or whitespace.")
            .Validate(o => o.Expiration > TimeSpan.Zero, "Expiration must be positive.")
            .Validate(o => o.JsonSerializerOptions is not null, "JsonSerializerOptions cannot be null.")
            .Validate(o => o.MaxIdempotencyKeyLength >= 1, "MaxIdempotencyKeyLength must be at least 1.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.IdempotencyKeyPattern), "IdempotencyKeyPattern cannot be empty.")
            .Validate(o => o.MinStatusCodeForCaching >= 100, "MinStatusCodeForCaching must be >= 100.")
            .Validate(o => o.MaxStatusCodeForCaching <= 599, "MaxStatusCodeForCaching must be <= 599.")
            .Validate(o => o.MinStatusCodeForCaching <= o.MaxStatusCodeForCaching,
                "MinStatusCodeForCaching cannot be greater than MaxStatusCodeForCaching.")
            .ValidateOnStart();

        return services;
    }

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

    #endregion
}