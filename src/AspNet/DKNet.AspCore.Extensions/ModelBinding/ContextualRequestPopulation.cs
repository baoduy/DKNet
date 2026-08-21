// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ContextualRequestPopulation.cs
// Description: Discovers IContextualSource-declared request members, populates them via registered resolvers,
//              and the DI registration that wires the mechanism up.

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using DKNet.AspCore.Extensions.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.AspCore.Extensions.ModelBinding;

/// <summary>
///     Options for <see cref="ContextualRequestPopulationServiceCollectionExtensions.AddContextualRequestPopulation" />.
/// </summary>
public sealed class ContextualPopulationOptions
{
    /// <summary>
    ///     Value substituted for a declared member the registered resolver could not resolve, but only when the
    ///     host's <see cref="EndpointRegistrationOptions.RequireAuthorization" /> is <see langword="false" />.
    ///     An authenticated-but-unresolved member (e.g. authorization on, claim missing) never receives this
    ///     value — it holds its type's default instead. <see langword="null" /> disables the fallback.
    /// </summary>
    public string? SystemAccountFallback { get; set; }
}

/// <summary>
///     Populates a bound request's <see cref="IContextualSource" />-declared members. Registered by
///     <see cref="ContextualRequestPopulationServiceCollectionExtensions.AddContextualRequestPopulation" /> and
///     applied automatically by <see cref="EndpointConfigExtensions.UseEndpointConfigs" />.
/// </summary>
internal interface IContextualRequestPopulationService
{
    /// <summary>
    ///     Overwrites every <see cref="IContextualSource" />-declared member on <paramref name="request" />. A
    ///     no-op when <paramref name="request" />'s type declares none.
    /// </summary>
    /// <param name="request">The bound request instance (a method argument of a mapped endpoint).</param>
    /// <param name="httpContext">The current request's <see cref="HttpContext" />.</param>
    /// <param name="requireAuthorization">
    ///     The host's <see cref="EndpointRegistrationOptions.RequireAuthorization" /> setting for the group this
    ///     request was bound on — drives whether an unresolved member falls back to
    ///     <see cref="ContextualPopulationOptions.SystemAccountFallback" />.
    /// </param>
    void Populate(object request, HttpContext httpContext, bool requireAuthorization);
}

/// <inheritdoc cref="IContextualRequestPopulationService" />
internal sealed class ContextualRequestPopulationService(
    IEnumerable<IContextualValueResolver> resolvers,
    ContextualPopulationOptions options) : IContextualRequestPopulationService
{
    public void Populate(object request, HttpContext httpContext, bool requireAuthorization)
    {
        var members = ContextualMemberScanner.GetDeclaredMembers(request.GetType());
        if (members.Length == 0) return;

        foreach (var member in members)
        {
            var raw = resolvers.FirstOrDefault(r => r.CanResolve(member.Source))?.Resolve(member.Source, httpContext);

            if (raw is null && !requireAuthorization && options.SystemAccountFallback is not null)
                raw = options.SystemAccountFallback;

            member.Property.SetValue(request, ConvertOrDefault(raw, member.Property.PropertyType));
        }
    }

    private static object? ConvertOrDefault(string? raw, Type targetType)
    {
        if (raw is null) return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

        try
        {
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            return TypeDescriptor.GetConverter(underlyingType).ConvertFromInvariantString(raw);
        }
        catch (Exception ex) when (ex is FormatException or NotSupportedException or ArgumentException)
        {
            // Not convertible to the member's type: hold its default, never reject the request — population is
            // not validation.
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }
    }
}

/// <summary>Declared member discovered on a request type: the property plus its <see cref="IContextualSource" /> declaration.</summary>
internal readonly record struct ContextualMember(PropertyInfo Property, IContextualSource Source);

/// <summary>
///     Discovers, and caches per <see cref="Type" />, the <see cref="IContextualSource" />-declared properties on a
///     request type. Also the startup-time fail-fast: a declared property with no setter (so binding's result
///     could never be overwritten) throws the first time its type is scanned.
/// </summary>
internal static class ContextualMemberScanner
{
    private static readonly ConcurrentDictionary<Type, ContextualMember[]> Cache = new();

    /// <summary>
    ///     Gets the cached, validated declared members for <paramref name="type" />. Calling this for every mapped
    ///     endpoint's parameter types at endpoint-build time is what makes an unassignable declaration fail at
    ///     startup rather than on first request.
    /// </summary>
    public static ContextualMember[] GetDeclaredMembers(Type type) => Cache.GetOrAdd(type, Discover);

    private static ContextualMember[] Discover(Type type)
    {
        var declared = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (Property: p, Source: p.GetCustomAttributes(true).OfType<IContextualSource>().FirstOrDefault()))
            .Where(x => x.Source is not null)
            .ToArray();

        foreach (var (property, _) in declared)
            if (property.SetMethod is null)
                throw new InvalidOperationException(
                    $"'{type.FullName}.{property.Name}' declares a contextual source but has no setter, so the " +
                    "population mechanism cannot assign it after binding. Add a setter (a plain 'set' or 'init').");

        return [.. declared.Select(x => new ContextualMember(x.Property, x.Source!))];
    }
}

/// <summary>Registers the contextual request population mechanism on <see cref="IServiceCollection" />.</summary>
public static class ContextualRequestPopulationServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the contextual request population mechanism: <see cref="IContextualSource" />-declared
    ///     request members (e.g. <see cref="FromClaimAttribute" />) are populated before validation and before
    ///     the handler runs, and excluded from the published OpenAPI description. Endpoint groups mapped by
    ///     <see cref="EndpointConfigExtensions.UseEndpointConfigs" /> apply this automatically once registered —
    ///     no per-group wiring is required. Requests with no declared members are unaffected.
    /// </summary>
    /// <param name="services">The service collection to register against.</param>
    /// <param name="configure">Configures <see cref="ContextualPopulationOptions" />; leave <see langword="null" /> for defaults.</param>
    /// <returns><paramref name="services" />, for chaining.</returns>
    public static IServiceCollection AddContextualRequestPopulation(
        this IServiceCollection services,
        Action<ContextualPopulationOptions>? configure = null)
    {
        var options = new ContextualPopulationOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        // Scoped, not singleton: a host may register its own IContextualValueResolver that depends on a scoped
        // service (e.g. a tenant resolver over a DbContext) — a singleton ContextualRequestPopulationService
        // capturing it would throw under scope validation or captive-dependency it in production.
        // ClaimValueResolver itself is stateless either way.
        services.AddScoped<IContextualValueResolver, ClaimValueResolver>();
        services.AddScoped<IContextualRequestPopulationService, ContextualRequestPopulationService>();

        services.ConfigureAll<OpenApiOptions>(o =>
        {
            o.AddSchemaTransformer<ContextualSourceSchemaTransformer>();
            o.AddOperationTransformer<ContextualSourceOperationTransformer>();
        });

        return services;
    }
}
