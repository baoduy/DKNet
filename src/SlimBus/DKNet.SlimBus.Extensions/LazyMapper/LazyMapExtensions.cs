// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: LazyMapExtensions.cs
// Description: Extension methods on IMapper to create lazily mapped values and FluentResults-compatible results.

using FluentResults;
using MapsterMapper;

namespace DKNet.SlimBus.Extensions.LazyMapper;

/// <summary>
///     Provides extension methods on <see cref="IMapper" /> to lazily map values to another type.
/// </summary>
public static class LazyMapExtensions
{
    #region Methods

    /// <summary>
    ///     Wraps <paramref name="value" /> in an <see cref="ILazyMap{TValue}" /> that maps it to
    ///     <typeparamref name="TValue" /> via <paramref name="mapper" /> only when the value is first accessed.
    /// </summary>
    /// <typeparam name="TValue">The target type to map to.</typeparam>
    /// <param name="mapper">The Mapster mapper used to perform the mapping.</param>
    /// <param name="value">The source value to map. May be <c>null</c>.</param>
    /// <returns>An <see cref="ILazyMap{TValue}" /> wrapping the deferred mapping.</returns>
    public static ILazyMap<TValue> LazyMap<TValue>(this IMapper mapper, object value) =>
        new LazyMap<TValue>(value, mapper);

    /// <summary>
    ///     Wraps <paramref name="value" /> in a successful <see cref="IResult{TValue}" /> whose value is lazily
    ///     mapped to <typeparamref name="TValue" /> via <paramref name="mapper" /> only when first accessed.
    /// </summary>
    /// <typeparam name="TValue">The target type to map to.</typeparam>
    /// <param name="mapper">The Mapster mapper used to perform the mapping.</param>
    /// <param name="value">The source value to map. May be <c>null</c>.</param>
    /// <returns>An <see cref="IResult{TValue}" /> wrapping the deferred mapping.</returns>
    public static IResult<TValue> ResultOf<TValue>(this IMapper mapper, object value) =>
        new LazyResult<TValue>(value, mapper);

    #endregion
}
