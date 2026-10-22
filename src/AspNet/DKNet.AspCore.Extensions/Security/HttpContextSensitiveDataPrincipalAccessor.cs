// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: HttpContextSensitiveDataPrincipalAccessor.cs
// Description: ISensitiveDataPrincipalAccessor backed by the current HttpContext — the signed-in user of the
// request, and no user outside a request.

using System.Security.Claims;
using DKNet.EfCore.Extensions.Serialization;
using Microsoft.AspNetCore.Http;

namespace DKNet.AspCore.Extensions.Security;

/// <summary>
///     Gives role-aware sensitive-data serialization the signed-in user of the current request.
/// </summary>
/// <param name="httpContextAccessor">The accessor of the current request.</param>
internal sealed class HttpContextSensitiveDataPrincipalAccessor(IHttpContextAccessor httpContextAccessor)
    : ISensitiveDataPrincipalAccessor
{
    /// <summary>The user of the current request; <see langword="null" /> outside a request.</summary>
    public ClaimsPrincipal? Current => httpContextAccessor.HttpContext?.User;
}
