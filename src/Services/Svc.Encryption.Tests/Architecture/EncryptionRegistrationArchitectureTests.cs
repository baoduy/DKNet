// <copyright file="EncryptionRegistrationArchitectureTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.Svc.Encryption;
using DKNet.Svc.Encryption.Ciphers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Svc.Encryption.Tests.Architecture;

/// <summary>
///     Guards the key lifetime of every cipher reachable through DI.
///     A cipher resolved from DI must return the same key on every resolution: a consumer encrypts through
///     one injected instance and decrypts through another on a later request, so a key that is regenerated
///     per resolution makes the ciphertext permanently unreadable — the key that produced it was never
///     persisted and dies with the instance. That is silent data loss, not a failed call.
///     <para>
///         DRK-900 closed that hole structurally rather than per-cipher: <c>AddEncryptionServices()</c> now
///         registers only the keyless hashing services, and every key-bearing cipher is reachable solely through
///         an <c>Add…Encryption(key)</c> overload that forces the caller to supply a durable key. This class
///         guards that structure. The key stability of the explicit registrations themselves is covered by
///         <c>EncryptionSetupAesTests</c> and <c>EdgeCaseTests</c>.
///     </para>
/// </summary>
public sealed class EncryptionRegistrationArchitectureTests
{
    #region Methods

    /// <summary>
    ///     <c>AddEncryptionServices()</c> must not register any key-bearing cipher. It once registered them with
    ///     throwaway per-resolution keys (DRK-79, DRK-900); each now requires the caller to supply the key through
    ///     <c>AddRsaEncryption</c> / <c>AddAesGcmEncryption</c>. Re-adding a keyless registration here would
    ///     reintroduce the same unrecoverable-ciphertext defect.
    /// </summary>
    [Theory]
    [InlineData(typeof(IRsaEncryption))]
    [InlineData(typeof(IAesGcmEncryption))]
    public void AddEncryptionServices_ShouldNotRegisterKeyBearingCipher(Type cipherServiceType)
    {
        var services = new ServiceCollection().AddEncryptionServices();

        services.Any(s => s.ServiceType == cipherServiceType).ShouldBeFalse(
            $"{cipherServiceType.Name} must be registered only through its Add…Encryption(key) overload, which " +
            "forces the caller to supply a durable key. A keyless registration here hands every resolution a fresh " +
            "random key, so anything it encrypts or signs can never be decrypted or verified again.");
    }

    #endregion
}
