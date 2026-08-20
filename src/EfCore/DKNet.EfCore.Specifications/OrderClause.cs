// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: OrderClause.cs
// Description: A single ordering step (key selector + direction) preserving the sequence in which it was declared.

using System.ComponentModel;
using System.Linq.Expressions;

namespace DKNet.EfCore.Specifications;

/// <summary>
///     Represents a single ordering step declared on a <see cref="Specification{TEntity}" />, pairing the key
///     selector with its sort direction so mixed-direction ordering can be applied in declaration order.
/// </summary>
/// <typeparam name="TEntity">Type of the entity.</typeparam>
/// <param name="KeySelector">The expression selecting the value to order by.</param>
/// <param name="Direction">The direction to order by.</param>
internal readonly record struct OrderClause<TEntity>(
    Expression<Func<TEntity, object>> KeySelector,
    ListSortDirection Direction);
