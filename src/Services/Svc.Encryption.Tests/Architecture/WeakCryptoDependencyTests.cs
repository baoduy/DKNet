// <copyright file="WeakCryptoDependencyTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.Svc.Encryption.Ciphers;
using NetArchTest.Rules;
using Shouldly;

namespace Svc.Encryption.Tests.Architecture;

/// <summary>
///     Keeps broken cryptographic primitives out of <c>DKNet.Svc.Encryption</c>.
///     MD5, SHA-1, DES, Triple DES and RC2 are all collision- or key-length-broken and must never appear on a
///     security path; <see cref="System.Random" /> is not a cryptographic generator and produces predictable
///     keys, nonces and salts. A downstream service trusts this package blindly — if one of these reaches a
///     key, nonce, salt or MAC here, the failure is silent everywhere it is consumed.
///     <para>
///         Tier-1 rule (architecture-review DRK-1567): the assembly is clean today and this locks that in.
///         <see cref="System.Security.Cryptography.SHA256" /> and friends stay allowed — the ban is on the
///         broken primitives only, not on hashing.
///     </para>
/// </summary>
public sealed class WeakCryptoDependencyTests
{
    #region Fields

    /// <summary>
    ///     Broken primitives, by full type name as NetArchTest sees the dependency.
    /// </summary>
    private static readonly string[] BannedPrimitives =
    [
        "System.Security.Cryptography.MD5",
        "System.Security.Cryptography.SHA1",
        "System.Security.Cryptography.DES",
        "System.Security.Cryptography.TripleDES",
        "System.Security.Cryptography.RC2",
        "System.Random"
    ];

    #endregion

    #region Methods

    [Fact]
    public void EncryptionLibrary_MustNotDependOnBrokenCryptographicPrimitives()
    {
        var result = Types.InAssembly(typeof(AesGcmEncryption).Assembly)
            .Should()
            .NotHaveDependencyOnAny(BannedPrimitives)
            .GetResult();

        var offenders = result.FailingTypeNames ?? [];
        result.IsSuccessful.ShouldBeTrue(
            "MD5/SHA-1/DES/TripleDES/RC2 are broken and System.Random is not cryptographically secure, so a " +
            "key, nonce, salt or MAC derived from one is guessable by anyone who knows it was used — and every " +
            "consumer of this package inherits that silently. Offenders: " + string.Join(", ", offenders) +
            ". Use AES-GCM, SHA-256 or better, and RandomNumberGenerator; do not add the type to an allow-list.");
    }

    [Fact]
    public void Rule_CanDetectABrokenPrimitive_OnADeliberateFixture()
    {
        // Self-check: WeakCryptoCanaryFixture (test-only) deliberately depends on MD5. If this ever passes,
        // NetArchTest can no longer see the dependency and the rule above silently stops enforcing anything.
        var result = Types.InAssembly(typeof(WeakCryptoCanaryFixture).Assembly)
            .That()
            .HaveName(nameof(WeakCryptoCanaryFixture))
            .Should()
            .NotHaveDependencyOnAny(BannedPrimitives)
            .GetResult();

        result.IsSuccessful.ShouldBeFalse(
            "The deliberately-offending WeakCryptoCanaryFixture must still be detected as depending on MD5; " +
            "if this assertion fails the enforcement rule can no longer see weak-primitive usage and is worthless.");
    }

    #endregion
}
