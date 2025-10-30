// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: GuidV7ValueGenerator.cs
// Description: Generates GUIDv7-style identifiers (time-ordered) suitable for EF Core value generation.

using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace DKNet.EfCore.Extensions.Convertors;

/// <summary>
///     A ValueGenerator that produces GUIDv7-style values. GUIDv7 encodes the Unix epoch milliseconds
///     into the high-order bytes so generated GUIDs are lexicographically (and mostly) time-ordered.
///     This generator uses a cryptographic random source for the non-timestamp bytes and sets the
///     version (7) and RFC 4122 variant bits appropriately.
/// </summary>
public sealed class GuidV7ValueGenerator : ValueGenerator<Guid>
{
    #region Properties

    /// <summary>
    ///     Indicates that generated values are final (not temporary) and will be persisted to the database.
    /// </summary>
    public override bool GeneratesTemporaryValues => false;

    #endregion

    #region Methods

    /// <summary>
    ///     Generates the next GUIDv7 value for the given EF Core entity entry.
    /// </summary>
    /// <param name="entry">The EF Core <see cref="EntityEntry" /> requesting a value. Can be <c>null</c> in some scenarios.</param>
    /// <returns>
    ///     A new <see cref="Guid" /> containing a 48-bit Unix epoch milliseconds prefix and 80 bits of randomness,
    ///     with version and variant bits set for GUIDv7/RFC4122 compatibility.
    /// </returns>
    public override Guid Next(EntityEntry entry)
    {
        // 16 bytes total: 6 bytes timestamp (ms since Unix epoch, big-endian), 10 bytes random
        Span<byte> bytes = stackalloc byte[16];

        // 48-bit timestamp (milliseconds)
        var unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Write big-endian timestamp into bytes[0..6)
        bytes[0] = (byte)(unixMs >> 40);
        bytes[1] = (byte)(unixMs >> 32);
        bytes[2] = (byte)(unixMs >> 24);
        bytes[3] = (byte)(unixMs >> 16);
        bytes[4] = (byte)(unixMs >> 8);
        bytes[5] = (byte)unixMs;

        // Fill remaining 10 bytes with cryptographic randomness
        RandomNumberGenerator.Fill(bytes.Slice(6, 10));

        // Set version to 7 (top 4 bits of bytes[6])
        bytes[6] = (byte)((bytes[6] & 0x0F) | (7 << 4));

        // Set the RFC 4122 variant (10xxxxxx) in the top two bits of bytes[8]
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }

    #endregion
}