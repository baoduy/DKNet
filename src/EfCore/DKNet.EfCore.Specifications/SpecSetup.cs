#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: SpecSetup.cs
// Description: DI registration helper for the specification repository implementation.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.Specifications;

/// <summary>
///     Dependency-injection helpers for the specifications package.
/// </summary>
public static class SpecSetup
{
    #region Methods

    /// <summary>
    ///     Registers the specification repository for the provided <typeparamref name="TDbContext" /> type.
    ///     The repository is registered as a scoped service and resolves <see cref="IRepositorySpec" /> to
    ///     <see cref="RepositorySpec{TDbContext}" />.
    /// </summary>
    /// <typeparam name="TDbContext">The <see cref="DbContext" /> type the repository will use.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection" /> instance for chaining.</returns>
    public static IServiceCollection AddSpecRepo<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext =>
        services.AddScoped<IRepositorySpec, RepositorySpec<TDbContext>>()
            .AddSingleton<IRepositorySpecFactory,RepositorySpecFactory>();

    #endregion
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member