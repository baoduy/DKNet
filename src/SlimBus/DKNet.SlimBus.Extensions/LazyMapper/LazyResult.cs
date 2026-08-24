// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: LazyResult.cs
// Description: A LazyMap that also implements FluentResults' IResult, so a lazily mapped value can be returned as a handler result.

using FluentResults;
using MapsterMapper;

namespace DKNet.SlimBus.Extensions.LazyMapper;

internal class LazyResult<TResult>(object? originalValue, IMapper mapper)
    : LazyMap<TResult>(originalValue, mapper), IResult<TResult>
{
    #region Properties

    public bool IsFailed => Reasons.OfType<IError>().Any();

    public bool IsSuccess => !IsFailed;

    public IReadOnlyList<IError> Errors => [.. Reasons.OfType<IError>()];

    public IReadOnlyList<ISuccess> Successes => [.. Reasons.OfType<ISuccess>()];

    public List<IReason> Reasons { get; init; } = [];

    #endregion
}
