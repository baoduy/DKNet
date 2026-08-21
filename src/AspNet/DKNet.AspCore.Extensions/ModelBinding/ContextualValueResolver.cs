// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ContextualValueResolver.cs
// Description: Resolver contract for IContextualSource declarations, plus the built-in claim resolver.

using Microsoft.AspNetCore.Http;

namespace DKNet.AspCore.Extensions.ModelBinding;

/// <summary>
///     Resolves an <see cref="IContextualSource" /> declaration to a raw value. The population mechanism picks a
///     resolver by asking each one registered in DI whether it can resolve a given declaration — never by the
///     declaration's concrete attribute type — so a new source kind needs only its own attribute plus a matching
///     resolver registered alongside it; no other mechanism code changes.
/// </summary>
public interface IContextualValueResolver
{
    /// <summary>Whether this resolver handles <paramref name="source" />.</summary>
    /// <param name="source">The declaration attribute instance found on the request property.</param>
    bool CanResolve(IContextualSource source);

    /// <summary>
    ///     Resolves <paramref name="source" /> against <paramref name="httpContext" />, or <see langword="null" />
    ///     when no value is available (e.g. the claim is missing).
    /// </summary>
    /// <param name="source">The declaration attribute instance found on the request property.</param>
    /// <param name="httpContext">The current request's <see cref="HttpContext" />.</param>
    string? Resolve(IContextualSource source, HttpContext httpContext);
}

/// <summary>
///     Built-in <see cref="IContextualValueResolver" /> that resolves <see cref="FromClaimAttribute" />
///     declarations from the current request's authenticated <see cref="HttpContext.User" />.
/// </summary>
internal sealed class ClaimValueResolver : IContextualValueResolver
{
    public bool CanResolve(IContextualSource source) => source is FromClaimAttribute;

    public string? Resolve(IContextualSource source, HttpContext httpContext) =>
        httpContext.User.FindFirst(((FromClaimAttribute)source).ClaimType)?.Value;
}
