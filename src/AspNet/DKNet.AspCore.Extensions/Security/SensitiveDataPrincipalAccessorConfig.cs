// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: SensitiveDataPrincipalAccessorConfig.cs
// Description: Registers the HttpContext-backed ISensitiveDataPrincipalAccessor that role-aware sensitive-data
// serialization judges the caller with.

using DKNet.EfCore.Extensions.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.AspCore.Extensions.Security;

/// <summary>
///     Registers an <see cref="ISensitiveDataPrincipalAccessor" /> that gives role-aware sensitive-data
///     serialization the signed-in user of the current request, and no user outside a request.
/// </summary>
public static class SensitiveDataPrincipalAccessorConfig
{
    /// <summary>
    ///     Registers <c>IHttpContextAccessor</c> and the request-backed <see cref="ISensitiveDataPrincipalAccessor" />,
    ///     keeping an accessor the app registered first. Opting the JSON options into
    ///     <see cref="SensitiveDataJsonExtensions.UseRoleAwareSensitiveData" /> stays the host's step.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same <see cref="IServiceCollection" />, for chaining.</returns>
    public static IServiceCollection AddSensitiveDataPrincipalAccessor(this IServiceCollection services) =>
        throw new NotImplementedException();
}
