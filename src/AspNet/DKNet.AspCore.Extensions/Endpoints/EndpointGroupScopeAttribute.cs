// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: EndpointGroupScopeAttribute.cs
// Description: Declares the permission scope an endpoint group requires for one or more HTTP methods.

namespace DKNet.AspCore.Extensions.Endpoints;

/// <summary>
///     Declares, above an <see cref="IEndpointConfig" /> class, the scope required for one or more of the HTTP
///     methods the group serves. Several declarations may be stacked on one group, each naming its own scope and
///     methods (see <see cref="EndpointHttpMethods" />). Applied only when
///     <see cref="EndpointRegistrationOptions.RequireAuthorization" /> is <see langword="true" /> — the only
///     public way for a package user to require a scope on a group.
/// </summary>
/// <param name="scope">The authorization scope required for <paramref name="httpMethods" />.</param>
/// <param name="httpMethods">The HTTP methods (see <see cref="EndpointHttpMethods" />) that require <paramref name="scope" />.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class EndpointGroupScopeAttribute(string scope, params string[] httpMethods) : Attribute
{
    /// <summary>The authorization scope required for <see cref="HttpMethods" />.</summary>
    public string Scope { get; } = scope;

    /// <summary>The HTTP methods that require <see cref="Scope" />.</summary>
    public string[] HttpMethods { get; } = httpMethods;
}
