// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: RequestBase.cs
// Description: Shared base record for SlimBus requests that need to carry the acting user.

using System.Text.Json.Serialization;

namespace DKNet.SlimBus.Extensions;

/// <summary>
///     Base record for requests that need to carry the acting user's identity. A consuming host's endpoint
///     registration is expected to populate <see cref="ByUser" /> from the authenticated principal (or a configured
///     system-account fallback) before the request reaches its handler.
/// </summary>
[Obsolete(
    "Superseded by declaring an IContextualSource attribute (e.g. [FromClaim(ClaimTypes.Name)] from " +
    "DKNet.AspCore.Extensions) directly on the request's own acting-user property, populated automatically via " +
    "AddContextualRequestPopulation(). RequestBase.ByUser is retained for existing consumers and is never " +
    "populated by this package.")]
public record RequestBase
{
    /// <summary>
    ///     The identity of the user acting on this request. Excluded from JSON (de)serialization so it can only be
    ///     set by the host, never supplied by the caller.
    /// </summary>
    [JsonIgnore]
    public string? ByUser { get; set; }
}
