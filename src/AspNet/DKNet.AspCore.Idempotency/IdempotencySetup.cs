using System.Diagnostics.CodeAnalysis;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    ///         A named store registered through this overload always wins: if no store is registered yet it is
    ///         registered as usual; if the parameterless <see cref="AddIdempotentKey(IServiceCollection,Action{IdempotencyOptions}?)" />
    ///         default (<see cref="IdempotencyInMemoryStore" />) is already registered, it is replaced by
    ///         <typeparamref name="TSoreImplement" />; if a different named store is already registered, that
    ///         earlier registration wins and this call is a no-op - first-registration-wins between two named
    ///         stores is unchanged. Where a default registration is replaced this way, the <paramref name="config" />
    ///         passed here is applied after the default's own configuration, so it decides any option both set.
    ///     </para>
    /// </remarks>
    public static IServiceCollection AddIdempotentKey<TSoreImplement>(this IServiceCollection services,
        Action<IdempotencyOptions>? config = null) where TSoreImplement : class, IIdempotencyKeyStore
    {
        var existing = services.FirstOrDefault(s => s.ServiceType == typeof(IIdempotencyKeyStore));
        if (existing is not null)
        {
            if (existing.ImplementationType != typeof(IdempotencyInMemoryStore))
                return services;

            services.Remove(existing);
        }

        services.AddSingleton<IIdempotencyKeyStore, TSoreImplement>();
        ConfigureIdempotencyOptions(services, config);

        return services;
    }

    /// <summary>
    ///     Registers idempotency services using the process-local, in-memory default store - no external
    ///     service, connection string, or type argument required.
    /// </summary>
    /// <param name="services">The service collection to register idempotency services into.</param>
    /// <param name="config">
    ///     An optional action to configure <see cref="IdempotencyOptions" />.
    ///     If null, default options are used.
    /// </param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    ///     Registers <see cref="IdempotencyInMemoryStore" /> as the <see cref="IIdempotencyKeyStore" /> and a
    ///     hosted service that logs a startup warning while it remains the resolved store. That store is
    ///     process-local and non-durable: idempotency keys are lost on restart and are not shared between
    ///     instances, so it is intended for local development and unit tests, never production.
    ///     <para>
    ///         A second call is a complete no-op (including its <paramref name="config" />) once an
    ///         <see cref="IIdempotencyKeyStore" /> is already registered - the first caller always wins. A named
    ///         store registered afterwards via
    ///         <see cref="AddIdempotentKey{TSoreImplement}(IServiceCollection,Action{IdempotencyOptions}?)" />
    ///         still replaces this default, whatever registration order.
    ///     </para>
    /// </remarks>
    public static IServiceCollection AddIdempotentKey(this IServiceCollection services,
        Action<IdempotencyOptions>? config = null)
    {
        if (services.IsRegistered<IIdempotencyKeyStore>())
            return services;

        services.AddSingleton<IIdempotencyKeyStore, IdempotencyInMemoryStore>();
        services.AddSingleton<IHostedService, IdempotencyInMemoryStoreWarning>();
        ConfigureIdempotencyOptions(services, config);

        return services;
    }

    /// <summary>
    ///     Registers <see cref="IdempotencyOptions" /> via the options pattern, applying <paramref name="config" />
    ///     (when supplied) and the shared validators, with <c>ValidateOnStart()</c> so a misconfiguration fails
    ///     fast at application startup rather than on first resolve. Shared by both <c>AddIdempotentKey</c>
    ///     overloads so the validator set is defined once.
    /// </summary>
    private static void ConfigureIdempotencyOptions(IServiceCollection services, Action<IdempotencyOptions>? config)
    {
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