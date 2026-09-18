// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: GroupScopeAuthorization.cs
// Description: Reads EndpointGroupScopeAttribute declarations and turns them into a route's authorization policy.

using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace DKNet.AspCore.Extensions.Endpoints;

/// <summary>
///     Turns <see cref="EndpointGroupScopeAttribute" /> declarations into per-route authorization policies. Not
///     public: the attribute is the only way a package user requires a scope on a group.
/// </summary>
internal static class GroupScopeAuthorization
{
    /// <summary>Per-group accumulation of the method-to-scope map built up across every declaration on that group.</summary>
    private static readonly ConditionalWeakTable<RouteGroupBuilder, Dictionary<string, string>> MethodScopes = new();

    /// <summary>
    ///     Records that <paramref name="httpMethods" /> on <paramref name="group" /> require <paramref name="scope" />.
    ///     The first call for a given <paramref name="group" /> registers the single <c>Finally</c> convention that
    ///     applies the accumulated map — and refuses an uncovered method — when the group's endpoints are built.
    /// </summary>
    internal static void DeclareGroupScope(RouteGroupBuilder group, string scope, params string[] httpMethods)
    {
        var isFirstDeclaration = !MethodScopes.TryGetValue(group, out var methodToScope);
        if (isFirstDeclaration)
        {
            methodToScope = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            MethodScopes.Add(group, methodToScope);
        }

        foreach (var httpMethod in httpMethods) methodToScope![httpMethod] = scope;

        if (isFirstDeclaration)
            ((IEndpointConventionBuilder)group).Finally(endpointBuilder => Apply(endpointBuilder, methodToScope!));
    }

    /// <summary>
    ///     Applies <paramref name="methodToScope" /> to one built endpoint (R1, R4, R5): skips a route that already
    ///     names its own policy or allows anonymous access, refuses an endpoint serving a method with no declared
    ///     scope, else adds an <see cref="AuthorizeAttribute" /> per distinct scope its served methods require.
    /// </summary>
    private static void Apply(EndpointBuilder endpointBuilder, Dictionary<string, string> methodToScope)
    {
        var hasOwnPolicy = endpointBuilder.Metadata
            .OfType<IAuthorizeData>()
            .Any(authorizeData => !string.IsNullOrEmpty(authorizeData.Policy));
        if (hasOwnPolicy || endpointBuilder.Metadata.OfType<IAllowAnonymous>().Any()) return;

        var servedMethods = endpointBuilder.Metadata.OfType<IHttpMethodMetadata>().FirstOrDefault()?.HttpMethods;
        var routePattern = endpointBuilder is RouteEndpointBuilder routeEndpointBuilder
            ? routeEndpointBuilder.RoutePattern.RawText
            : endpointBuilder.DisplayName;

        if (servedMethods is null || servedMethods.Count == 0)
        {
            RequireCoverage(routePattern, "*", methodToScope, out _);
            return;
        }

        var scopesToApply = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in servedMethods)
        {
            RequireCoverage(routePattern, method, methodToScope, out var scope);
            scopesToApply.Add(scope);
        }

        foreach (var scope in scopesToApply) endpointBuilder.Metadata.Add(new AuthorizeAttribute(scope));
    }

    private static void RequireCoverage(
        string? routePattern,
        string method,
        Dictionary<string, string> methodToScope,
        out string scope)
    {
        if (methodToScope.TryGetValue(method, out scope!)) return;

        throw new InvalidOperationException(
            $"Route '{routePattern}' serves HTTP method '{method}' with no scope declared by an " +
            $"{nameof(EndpointGroupScopeAttribute)} above its group.");
    }
}
