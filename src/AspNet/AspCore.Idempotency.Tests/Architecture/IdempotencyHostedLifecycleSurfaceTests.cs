// <copyright file="IdempotencyHostedLifecycleSurfaceTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency.Store;

namespace AspCore.Idempotency.Tests.Architecture;

/// <summary>
///     DRK-1507 §5 "An application needs no source change": moving the startup warning onto
///     <c>IHostedLifecycleService</c> must not turn it into a consumer-visible type. This is a regression guard,
///     not a behaviour this change adds - it is already true today and must stay true after the migration
///     (§3 row 7; the migration hosted service's twin of this check lives in
///     <c>AspCore.Idempotency.NpgsqlStore.Tests</c>, the nearest project with visibility into the internal
///     <c>DKNet.AspCore.Idempotency.Relational</c> type - see that project's report note).
/// </summary>
public sealed class IdempotencyHostedLifecycleSurfaceTests
{
    #region Methods

    [Fact]
    public void IdempotencyInMemoryStoreWarning_StaysInternal()
    {
        typeof(IdempotencyInMemoryStoreWarning).IsPublic.ShouldBeFalse();
        typeof(IdempotencyInMemoryStoreWarning).IsVisible.ShouldBeFalse();
    }

    #endregion
}
