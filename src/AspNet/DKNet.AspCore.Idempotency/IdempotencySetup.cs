using System.Diagnostics.CodeAnalysis;
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
    #region Properties

    /// <summary>
    ///     Gets the HTTP header name used for idempotency keys.
    ///     This value is set when <see cref="AddIdempotentKey" /> is called and can be used to reference
    ///     the header in documentation or client code.
    /// </summary>
    public static string IdempotentHeaderKey { get; private set; } = null!;

    #endregion

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
    ///     - <see cref="IdempotencyDistributedCacheStore" /> as the <see cref="IIdempotencyKeyStore" />
    ///     implementation
    ///     - Configuration options as a singleton
    /// </remarks>
    public static IServiceCollection AddIdempotentKey(this IServiceCollection services,
        Action<IdempotencyOptions>? config = null)
    {
        var options = new IdempotencyOptions();
        config?.Invoke(options);

        services
            .AddSingleton<IIdempotencyKeyStore, IdempotencyDistributedCacheStore>()
            .AddSingleton(Options.Create(options));

        IdempotentHeaderKey = options.IdempotencyHeaderKey;

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