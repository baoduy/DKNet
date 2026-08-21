// <copyright file="KeysetPage.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace DKNet.EfCore.Specifications.Extensions;

/// <summary>
///     A page of keyset-paginated results, reporting whether a further page exists ahead of and behind it.
/// </summary>
/// <typeparam name="TEntity">The entity type contained in the page.</typeparam>
/// <param name="Items">The rows returned for this page, in declared keyset order.</param>
/// <param name="HasPrevious">Whether a further page exists behind (before) this one.</param>
/// <param name="HasNext">Whether a further page exists ahead of (after) this one.</param>
public sealed record KeysetPage<TEntity>(IReadOnlyList<TEntity> Items, bool HasPrevious, bool HasNext);
