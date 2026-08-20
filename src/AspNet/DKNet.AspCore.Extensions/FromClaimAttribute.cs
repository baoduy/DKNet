// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: FromClaimAttribute.cs
// Description: Declares that a request property is populated from a single named claim on the authenticated caller.

namespace DKNet.AspCore.Extensions;

/// <summary>
///     Declares that the decorated property is populated, before validation and before the handler runs, from
///     the named claim on the authenticated caller. A caller-supplied value for this property is always
///     overwritten — including with the property's default value when the claim is missing or the caller is
///     unauthenticated — so it can never be forged through the request payload. Requires
///     <see cref="ContextualRequestPopulationServiceCollectionExtensions.AddContextualRequestPopulation" /> to be
///     registered; population is otherwise inert.
/// </summary>
/// <param name="claimType">
///     The claim type to read, e.g. <see cref="System.Security.Claims.ClaimTypes.Name" />.
/// </param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FromClaimAttribute(string claimType) : Attribute, IContextualSource
{
    /// <summary>The claim type this property's value is resolved from.</summary>
    public string ClaimType { get; } = claimType ?? throw new ArgumentNullException(nameof(claimType));
}
