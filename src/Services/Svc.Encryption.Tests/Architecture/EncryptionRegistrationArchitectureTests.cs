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
///     Guards the key lifetime of every cipher <c>AddEncryptionServices()</c> registers.
///     A cipher resolved from DI must return the same key on every resolution: a consumer encrypts through
///     one injected instance and decrypts through another on a later request, so a key that is regenerated
///     per resolution makes the ciphertext permanently unreadable — the key that produced it was never
///     persisted and dies with the instance. That is silent data loss, not a failed call.
/// </summary>
public sealed class EncryptionRegistrationArchitectureTests
{
    #region Fields

    /// <summary>
    ///     Cipher services registered today with a per-resolution random key (DRK-900). This allow-list may only
    ///     shrink: delete an entry when its registration is fixed to take an explicit key. Do not add to it —
    ///     a new entry means a new cipher shipped with the same data-loss defect.
    /// </summary>
    private static readonly HashSet<Type> KnownViolations =
    [
#pragma warning disable CS0618 // AES-CBC cipher is obsolete but still registered for backward compatibility.
        typeof(IAesEncryption),
#pragma warning restore CS0618
        typeof(IAesGcmEncryption)
    ];

    #endregion

    #region Methods

    /// <summary>
    ///     Every key-bearing cipher registered by <see cref="EncryptionSetup.AddEncryptionServices" /> must hand out
    ///     the same key on two resolutions, unless it is a known violation being worked off the backlog.
    /// </summary>
    [Theory]
#pragma warning disable CS0618 // AES-CBC cipher is obsolete but still registered for backward compatibility.
    [InlineData(typeof(IAesEncryption))]
#pragma warning restore CS0618
    [InlineData(typeof(IAesGcmEncryption))]
    public void RegisteredCipher_ShouldResolveWithAStableKey(Type serviceType)
    {
        using var provider = new ServiceCollection()
            .AddEncryptionServices()
            .BuildServiceProvider();

        var first = ReadKey(provider.GetRequiredService(serviceType));
        var second = ReadKey(provider.GetRequiredService(serviceType));

        var stable = string.Equals(first, second, StringComparison.Ordinal);

        if (KnownViolations.Contains(serviceType))
        {
            stable.ShouldBeFalse(
                $"{serviceType.Name} is on the KnownViolations allow-list but now resolves with a stable key. " +
                "That is the fix landing — delete its entry from KnownViolations so the rule is enforced for it.");
            return;
        }

        stable.ShouldBeTrue(
            $"{serviceType.Name} resolves with a different key each time, so data encrypted through one injected " +
            "instance can never be decrypted through another — the ciphertext is unrecoverable. Register the " +
            "cipher with an explicit key from configuration or a key vault (see AddRsaEncryption for the shape) " +
            "instead of letting the container activate a key-generating constructor.");
    }

    /// <summary>
    ///     <c>AddEncryptionServices()</c> must not register <see cref="IRsaEncryption" />. It once did, with a
    ///     throwaway per-resolution key (DRK-79); RSA now requires the caller to supply the key through
    ///     <c>AddRsaEncryption(privateKeyBase64)</c>. Re-adding a keyless RSA registration would reintroduce
    ///     the same unrecoverable-ciphertext defect.
    /// </summary>
    [Fact]
    public void AddEncryptionServices_ShouldNotRegisterRsaEncryption()
    {
        var services = new ServiceCollection().AddEncryptionServices();

        services.Any(s => s.ServiceType == typeof(IRsaEncryption)).ShouldBeFalse(
            "IRsaEncryption must be registered only through AddRsaEncryption(privateKeyBase64), which forces the " +
            "caller to supply a durable key. A keyless registration here hands every resolution a fresh random " +
            "key, so anything it encrypts or signs can never be decrypted or verified again.");
    }

    private static string ReadKey(object cipher)
    {
        var key = cipher.GetType().GetProperty("Key")?.GetValue(cipher) as string;

        key.ShouldNotBeNullOrWhiteSpace(
            $"{cipher.GetType().Name} exposes no readable Key, so this rule cannot check its key lifetime. " +
            "Either expose the key or drop the type from this test's Theory data.");

        return key!;
    }

    #endregion
}
