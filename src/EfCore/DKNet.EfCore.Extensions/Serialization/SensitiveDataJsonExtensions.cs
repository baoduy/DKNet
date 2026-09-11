// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: SensitiveDataJsonExtensions.cs
// Description: Host opt-in that withholds a [SensitiveData]-declared property from a JSON response
// unless the caller is authenticated and holds one of the roles the property named.

using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DKNet.EfCore.Abstractions.Attributes;

namespace DKNet.EfCore.Extensions.Serialization;

/// <summary>
///     Adds role-aware filtering of <see cref="SensitiveDataAttribute" />-declared properties to a
///     <see cref="JsonSerializerOptions" />. Absent this opt-in, serialization is byte-for-byte identical
///     to today's behaviour — nothing is filtered by default.
/// </summary>
public static class SensitiveDataJsonExtensions
{
    /// <summary>
    ///     Opts a <see cref="JsonSerializerOptions" /> into role-aware filtering: every property carrying
    ///     <see cref="SensitiveDataAttribute" /> is withheld from the serialized payload unless
    ///     <paramref name="accessor" />'s <see cref="ISensitiveDataPrincipalAccessor.Current" /> is
    ///     authenticated and holds one of the attribute's named roles (any authenticated caller when no
    ///     role is named). A property without the attribute is never touched. The decision is re-evaluated
    ///     from <paramref name="accessor" /> on every serialization — it is never cached into the
    ///     <see cref="JsonTypeInfo" /> — so two callers sharing one <see cref="JsonSerializerOptions" />
    ///     are judged independently.
    /// </summary>
    /// <param name="options">
    ///     The options to modify. Composes with (does not replace) any <see cref="JsonSerializerOptions.TypeInfoResolver" />
    ///     already configured. Safe to call more than once; repeated calls compose additional, functionally
    ///     redundant modifiers rather than corrupting state. Must not already be frozen by a prior
    ///     serialize call — <see cref="JsonSerializerOptions" /> throws <see cref="InvalidOperationException" />
    ///     itself in that case, by design (fail loudly at opt-in time).
    /// </param>
    /// <param name="accessor">The seam supplying the caller's principal at serialization time.</param>
    /// <returns>The same <paramref name="options" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> or <paramref name="accessor" /> is <see langword="null" />.</exception>
    public static JsonSerializerOptions UseRoleAwareSensitiveData(
        this JsonSerializerOptions options, ISensitiveDataPrincipalAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(accessor);

        var innerResolver = options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver();
        options.TypeInfoResolver = innerResolver.WithAddedModifier(typeInfo => Modify(typeInfo, accessor));
        return options;
    }

    /// <summary>
    ///     Decides whether <paramref name="principal" /> may receive a property declared sensitive to
    ///     <paramref name="roles" />: fails closed when unauthenticated, admits any authenticated caller
    ///     when no role is named, otherwise requires membership in at least one named role.
    /// </summary>
    /// <param name="principal">The caller's principal, or <see langword="null" />.</param>
    /// <param name="roles">The roles named on the property's <see cref="SensitiveDataAttribute" />.</param>
    /// <returns><see langword="true" /> when the property should be serialized for this caller.</returns>
    internal static bool IsPermitted(ClaimsPrincipal? principal, IReadOnlyList<string> roles)
    {
        if (principal?.Identity?.IsAuthenticated != true)
            return false;

        if (roles.Count == 0)
            return true;

        // A plain loop avoids the per-call delegate + enumerator allocation `roles.Any(principal.IsInRole)`
        // would incur for every sensitive property of every serialized object.
        foreach (var role in roles)
        {
            if (principal.IsInRole(role))
                return true;
        }

        return false;
    }

    private static void Modify(JsonTypeInfo typeInfo, ISensitiveDataPrincipalAccessor accessor)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
            return;

        foreach (var property in typeInfo.Properties)
        {
            var sensitive = property.AttributeProvider?
                .GetCustomAttributes(typeof(SensitiveDataAttribute), true)
                .OfType<SensitiveDataAttribute>()
                .FirstOrDefault();

            if (sensitive is null)
                continue;

            var roles = sensitive.Roles;
            property.ShouldSerialize = (_, _) => IsPermitted(accessor.Current, roles);
        }
    }
}
