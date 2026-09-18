// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: GroupScopeAuthorization.cs
// Description: Reads EndpointGroupScopeAttribute declarations and turns them into a route's authorization policy.

using Microsoft.AspNetCore.Routing;

namespace DKNet.AspCore.Extensions.Endpoints;

/// <summary>
///     Turns <see cref="EndpointGroupScopeAttribute" /> declarations into per-route authorization policies. Not
///     public: the attribute is the only way a package user requires a scope on a group.
/// </summary>
internal static class GroupScopeAuthorization
{
    /// <summary>
    ///     Records that <paramref name="httpMethods" /> on <paramref name="group" /> require <paramref name="scope" />.
    /// </summary>
    /// <remarks>
    ///     Acceptance-tests stage stub (DRK-1544 D1542-1): accumulation and the build-time coverage check are not
    ///     implemented yet — see the Build sub-task. Deliberately a no-op rather than throwing
    ///     <see cref="NotImplementedException" />: <see cref="EndpointConfigExtensions.UseEndpointConfigs" /> scans
    ///     the whole calling assembly, so every <see cref="IEndpointConfig" /> fixture that carries this attribute
    ///     — including ones belonging to scenarios other than the one a given test exercises — gets mapped into
    ///     every test's host. A throw here would fail every such host's endpoint build, not just the host of the
    ///     test that added the fixture.
    /// </remarks>
    internal static void DeclareGroupScope(RouteGroupBuilder group, string scope, params string[] httpMethods)
    {
    }
}
