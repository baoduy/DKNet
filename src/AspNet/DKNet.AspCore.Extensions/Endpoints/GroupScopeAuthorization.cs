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
    /// <summary>Per-group accumulated state built up across every declaration on that group.</summary>
    private static readonly ConditionalWeakTable<RouteGroupBuilder, GroupScopeState> States = new();

    /// <summary>A group's per-method scope map plus its default scope (R7), if any.</summary>
    private sealed class GroupScopeState
    {
        internal readonly Dictionary<string, string> MethodToScope = new(StringComparer.OrdinalIgnoreCase);
        internal string? DefaultScope;
    }

    /// <summary>
    ///     Records that <paramref name="httpMethods" /> on <paramref name="group" /> require <paramref name="scope" />
    ///     — or, when <paramref name="httpMethods" /> is empty, that <paramref name="scope" /> is the group's default
    ///     (R7). The first call for a given <paramref name="group" /> registers the single <c>Finally</c> convention
    ///     that applies the accumulated state — and refuses an uncovered method — when the group's endpoints are built.
    /// </summary>
    internal static void DeclareGroupScope(RouteGroupBuilder group, string scope, params string[] httpMethods)
    {
        var isFirstDeclaration = !States.TryGetValue(group, out var state);
        if (isFirstDeclaration)
        {
            state = new GroupScopeState();
            States.Add(group, state);
        }

        if (httpMethods.Length == 0)
            state!.DefaultScope = scope;
        else
            foreach (var httpMethod in httpMethods) state!.MethodToScope[httpMethod] = scope;

        if (isFirstDeclaration)
            ((IEndpointConventionBuilder)group).Finally(endpointBuilder => Apply(endpointBuilder, state!));
    }

    /// <summary>
    ///     Applies <paramref name="state" /> to one built endpoint (R1, R4, R5): skips a route that already
    ///     names its own policy or allows anonymous access, refuses an endpoint serving a method with no declared
    ///     scope and no group default (R8), else adds an <see cref="AuthorizeAttribute" /> per distinct scope its
    ///     served methods require.
    /// </summary>
    private static void Apply(EndpointBuilder endpointBuilder, GroupScopeState state)
    {
        var hasOwnPolicy = endpointBuilder.Metadata
            .OfType<IAuthorizeData>()
            .Any(authorizeData => !string.IsNullOrEmpty(authorizeData.Policy));
        if (hasOwnPolicy || endpointBuilder.Metadata.OfType<IAllowAnonymous>().Any()) return;

        // Union of every IHttpMethodMetadata entry, not just one: EndpointMetadataCollection.GetMetadata<T>()
        // (what HttpMethodMatcherPolicy dispatches on) returns the LAST match, so reading only the first or last
        // entry can miss a method a route actually serves when more than one entry is stacked on it (e.g. a
        // second WithMetadata(new HttpMethodMetadata(...)) call). Same shape as
        // DKNet.AspCore.Idempotency.IdempotencySetup.RequiredIdempotentKey.
        var servedMethods = endpointBuilder.Metadata
            .OfType<IHttpMethodMetadata>()
            .SelectMany(metadata => metadata.HttpMethods)
            .ToList();
        var routePattern = endpointBuilder is RouteEndpointBuilder routeEndpointBuilder
            ? routeEndpointBuilder.RoutePattern.RawText
            : endpointBuilder.DisplayName;

        if (servedMethods.Count == 0)
        {
            RequireCoverage(routePattern, "*", state, out _);
            return;
        }

        var scopesToApply = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in servedMethods)
        {
            RequireCoverage(routePattern, method, state, out var scope);
            scopesToApply.Add(scope);
        }

        foreach (var scope in scopesToApply) endpointBuilder.Metadata.Add(new AuthorizeAttribute(scope));
    }

    private static void RequireCoverage(
        string? routePattern,
        string method,
        GroupScopeState state,
        out string scope)
    {
        if (state.MethodToScope.TryGetValue(method, out scope!)) return;

        if (state.DefaultScope is not null)
        {
            scope = state.DefaultScope;
            return;
        }

        throw new InvalidOperationException(
            $"Route '{routePattern}' serves HTTP method '{method}' with no scope declared by an " +
            $"{nameof(EndpointGroupScopeAttribute)} above its group.");
    }
}
