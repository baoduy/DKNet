// <copyright file="IdempotencyKeyPatternTimeoutTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Text.RegularExpressions;
using DKNet.AspCore.Idempotency;

namespace AspCore.Idempotency.Tests.Architecture;

/// <summary>
///     DRK-1571: the idempotency key-pattern regex must carry a bounded <see cref="Regex.MatchTimeout" /> so a
///     pathological pattern (default or operator-supplied) cannot hang the request thread via catastrophic
///     backtracking. Tier-1 enforcement guard - both the default construction path and the custom-pattern setter
///     must produce a regex with a finite timeout.
/// </summary>
public sealed class IdempotencyKeyPatternTimeoutTests
{
    #region Methods

    [Fact]
    public void DefaultOptions_KeyPatternRegex_HasBoundedMatchTimeout()
    {
        var options = new IdempotencyOptions();

        options.IdempotencyKeyPatternRegex.MatchTimeout.ShouldNotBe(Regex.InfiniteMatchTimeout);
    }

    [Fact]
    public void CustomKeyPattern_KeyPatternRegex_HasBoundedMatchTimeout()
    {
        var options = new IdempotencyOptions { IdempotencyKeyPattern = "^([a-zA-Z0-9]+)+$" };

        options.IdempotencyKeyPatternRegex.MatchTimeout.ShouldNotBe(Regex.InfiniteMatchTimeout);
    }

    #endregion
}
