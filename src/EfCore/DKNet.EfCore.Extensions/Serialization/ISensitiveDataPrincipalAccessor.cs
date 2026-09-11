// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ISensitiveDataPrincipalAccessor.cs
// Description: Seam that keeps DKNet.EfCore.Extensions free of any ASP.NET Core reference while still
// letting a host supply the caller identity role-aware sensitive-data filtering judges against.

using System.Security.Claims;

namespace DKNet.EfCore.Extensions.Serialization;

/// <summary>
///     Supplies the caller's <see cref="ClaimsPrincipal" /> to <see cref="SensitiveDataJsonExtensions" /> at
///     serialization time. The host implements this — typically by wrapping <c>IHttpContextAccessor</c> — so
///     this package never takes an ASP.NET Core dependency.
/// </summary>
public interface ISensitiveDataPrincipalAccessor
{
    /// <summary>
    ///     The current caller's principal, or <see langword="null" /> when no caller identity is available
    ///     (e.g. outside a request, or the host has not wired one up).
    /// </summary>
    ClaimsPrincipal? Current { get; }
}
