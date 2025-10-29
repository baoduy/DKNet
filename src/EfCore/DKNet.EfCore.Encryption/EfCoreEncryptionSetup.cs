// <copyright file="EfCoreEncryptionSetup.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Encryption.Encryption;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.Encryption;

/// <summary>
///     Provides extension methods for setting up Entity Framework Core column encryption services.
/// </summary>
public static class EfCoreEncryptionSetup
{
    #region Methods

    /// <summary>
    ///     Adds Entity Framework Core column encryption services to the service collection.
    /// </summary>
    /// <typeparam name="TKeyServiceImplementation">The type implementing <see cref="IEncryptionKeyProvider" />.</typeparam>
    /// <param name="services">The service collection to add encryption services to.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static ServiceCollection AddEfCoreEncryption<TKeyServiceImplementation>(this ServiceCollection services)
        where TKeyServiceImplementation : class, IEncryptionKeyProvider
    {
        services.AddSingleton<IEncryptionKeyProvider, TKeyServiceImplementation>();
        return services;
    }

    #endregion
}