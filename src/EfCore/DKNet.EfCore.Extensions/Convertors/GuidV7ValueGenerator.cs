// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: GuidV7ValueGenerator.cs
// Description: Generates GUIDv7-style identifiers (time-ordered) suitable for EF Core value generation.

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
    ///     A new <see cref="Guid" /> containing a 48-bit Unix epoch milliseconds prefix and 74 bits of randomness,
    ///     with version and variant bits set for GUIDv7/RFC4122 compatibility.
    /// </returns>
    public override Guid Next(EntityEntry entry) => Guid.CreateVersion7();

    #endregion
}