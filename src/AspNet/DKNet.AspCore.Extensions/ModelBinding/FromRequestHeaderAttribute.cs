// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: FromRequestHeaderAttribute.cs
// Description: Declares that a request property is populated from a single named HTTP request header.

namespace DKNet.AspCore.Extensions.ModelBinding;

/// <summary>
///     Declares that the decorated property is populated, before validation and before the handler runs, from
///     the named HTTP request header. A caller-supplied value for this property is always overwritten — including
///     with the property's default value when the header is missing — so it can never be forged through the
///     request payload. A missing header is never a refusal; it simply leaves the property's default. Unlike
///     <see cref="FromClaimAttribute" />, this source is never an authorization signal — a header
///     is caller-supplied and carries no identity guarantee. Requires
///     <see cref="ContextualRequestPopulationServiceCollectionExtensions.AddContextualRequestPopulation" /> to be
///     registered; population is otherwise inert.
/// </summary>
/// <param name="headerName">The HTTP request header name to read, matched case-insensitively.</param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FromRequestHeaderAttribute(string headerName) : Attribute, IContextualSource
{
    /// <summary>The HTTP request header name this property's value is resolved from.</summary>
    public string HeaderName { get; } = headerName ?? throw new ArgumentNullException(nameof(headerName));
}
