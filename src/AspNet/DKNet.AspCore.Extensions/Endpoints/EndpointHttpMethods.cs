// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: EndpointHttpMethods.cs
// Description: Compile-time HTTP method constants usable as EndpointGroupScopeAttribute arguments.

namespace DKNet.AspCore.Extensions.Endpoints;

/// <summary>
///     The HTTP method values an <see cref="EndpointGroupScopeAttribute" /> declaration may name. Published as
///     <see langword="const" /> fields — unlike <see cref="Microsoft.AspNetCore.Http.HttpMethods" />'
///     <see langword="static readonly" /> fields — so they can be used as attribute constructor arguments.
/// </summary>
public static class EndpointHttpMethods
{
    /// <summary>The HTTP GET method.</summary>
    public const string Get = "GET";

    /// <summary>The HTTP POST method.</summary>
    public const string Post = "POST";

    /// <summary>The HTTP PUT method.</summary>
    public const string Put = "PUT";

    /// <summary>The HTTP DELETE method.</summary>
    public const string Delete = "DELETE";

    /// <summary>The HTTP PATCH method.</summary>
    public const string Patch = "PATCH";

    /// <summary>The HTTP HEAD method.</summary>
    public const string Head = "HEAD";

    /// <summary>The HTTP OPTIONS method.</summary>
    public const string Options = "OPTIONS";
}
