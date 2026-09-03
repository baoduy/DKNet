using System.Diagnostics.CodeAnalysis;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
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
    ///     This method must be called before adding endpoint filters that use
    ///     <see cref="RequiredIdempotentKey(RouteHandlerBuilder)" />.
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
    ///         passed here is applied after the default's own configuration, so it decides any option both set -
    ///         while the shared validators, registered once for the container by the default's own call, are not
    ///         registered again.
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
    ///     (when supplied) on every call - so the latest caller's <paramref name="config" /> wins on any option
    ///     both registrations set - while the shared validators and <c>ValidateOnStart()</c> are registered at
    ///     most once per <paramref name="services" /> collection, so a misconfiguration fails fast at application
    ///     startup and is reported exactly once rather than once per <c>AddIdempotentKey</c> call. Shared by both
    ///     <c>AddIdempotentKey</c> overloads so the validator set is defined once.
    /// </summary>
    private static void ConfigureIdempotencyOptions(IServiceCollection services, Action<IdempotencyOptions>? config)
    {
        var optionsBuilder = services.AddOptions<IdempotencyOptions>();
        if (config is not null)
            optionsBuilder.Configure(config);

        if (services.Any(s => s.ServiceType == typeof(ValidatorsRegisteredMarker)))
            return;

        services.AddSingleton<ValidatorsRegisteredMarker>();

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
    ///     Marker type registered once per <see cref="IServiceCollection" /> to guard against registering the
    ///     <see cref="IdempotencyOptions" /> validators more than once. Carries no behaviour - its presence in
    ///     the collection is the only thing that matters.
    /// </summary>
    private sealed class ValidatorsRegisteredMarker;

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
    ///     Adds the idempotency endpoint filter to every endpoint in a route group whose routed HTTP verb
    ///     matches <paramref name="httpMethods" />, including endpoints in nested groups and endpoints
    ///     mapped into the group after this call.
    /// </summary>
    /// <param name="group">The route group builder to protect.</param>
    /// <param name="httpMethods">
    ///     The HTTP verbs to cover, matched case-insensitively against each endpoint's routed verb(s).
    ///     When omitted or empty, defaults to <c>POST</c> only.
    /// </param>
    /// <returns>The route group builder for method chaining.</returns>
    /// <remarks>
    ///     Coverage is decided once, at endpoint-build time, from the endpoint's <see cref="HttpMethodMetadata" />
    ///     — never from the incoming request (e.g. an <c>X-HTTP-Method-Override</c> header has no effect on
    ///     coverage). An endpoint with no <see cref="HttpMethodMetadata" /> at all (e.g. one mapped with
    ///     <c>app.Map(...)</c> with no verb constraint, which also accepts GET) is never covered — declare
    ///     explicit verbs on any endpoint you want protected.
    ///     <para>
    ///         An endpoint covered by both a group declaration and its own
    ///         <see cref="RequiredIdempotentKey(RouteHandlerBuilder)" /> call still runs the idempotency logic
    ///         exactly once; <see cref="IdempotencyEndpointFilter" /> guards against double invocation.
    ///     </para>
    ///     Typical usage:
    ///     <code>
    ///     var orders = app.MapGroup("/api/orders").RequiredIdempotentKey();                 // POST only
    ///     var admin  = app.MapGroup("/api/admin").RequiredIdempotentKey("POST", "DELETE");
    ///     </code>
    /// </remarks>
    public static RouteGroupBuilder RequiredIdempotentKey(this RouteGroupBuilder group, params string[] httpMethods)
    {
        var coveredMethods = httpMethods.Length > 0 ? httpMethods : ["POST"];

        // Not AddEndpointFilterFactory: its EndpointFilterFactoryContext exposes only MethodInfo and
        // ApplicationServices, with no way to read the endpoint's routed verbs. IEndpointConventionBuilder.Add
        // runs the same convention pipeline (AddEndpointFilterFactory is implemented on top of it) but hands
        // us the real EndpointBuilder, whose Metadata carries HttpMethodMetadata and whose FilterFactories is
        // the same list AddEndpointFilterFactory would have appended to.
        ((IEndpointConventionBuilder)group).Add(endpointBuilder =>
        {
            var routedMethods = endpointBuilder.Metadata
                .OfType<HttpMethodMetadata>()
                .SelectMany(m => m.HttpMethods);

            if (!routedMethods.Any(m => coveredMethods.Contains(m, StringComparer.OrdinalIgnoreCase)))
                return;

            endpointBuilder.FilterFactories.Add((factoryContext, next) =>
            {
                var filter = ActivatorUtilities.CreateInstance<IdempotencyEndpointFilter>(
                    factoryContext.ApplicationServices);

                return invocationContext => filter.InvokeAsync(invocationContext, next);
            });
        });

        return group;
    }

    #endregion
}