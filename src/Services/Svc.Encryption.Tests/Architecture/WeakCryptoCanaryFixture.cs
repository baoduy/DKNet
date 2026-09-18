// <copyright file="WeakCryptoCanaryFixture.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Security.Cryptography;

namespace Svc.Encryption.Tests.Architecture;

/// <summary>
///     Test-only canary. Deliberately depends on <see cref="MD5" /> so
///     <see cref="WeakCryptoDependencyTests.Rule_CanDetectABrokenPrimitive_OnADeliberateFixture" /> can prove the
///     NetArchTest rule still detects a banned primitive. Never reference this from production code, and never
///     "fix" it — a green canary means the real rule has stopped enforcing anything.
/// </summary>
internal static class WeakCryptoCanaryFixture
{
    #region Methods

    public static byte[] DeliberatelyWeakHash(byte[] input) => MD5.HashData(input);

    #endregion
}
