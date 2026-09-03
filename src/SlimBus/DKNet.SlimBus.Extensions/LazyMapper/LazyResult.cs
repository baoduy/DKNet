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
    #region Fields

    // Reasons is populated at construction (via the init accessor below) and never mutated afterwards,
    // so Errors/Successes can be memoised the first time they are read instead of re-walking Reasons
    // on every access.
    private IReadOnlyList<IError>? _errors;
    private IReadOnlyList<ISuccess>? _successes;

    #endregion

    #region Properties

    public bool IsFailed => Reasons.Exists(static r => r is IError);

    public bool IsSuccess => !IsFailed;

    public IReadOnlyList<IError> Errors => _errors ??= [.. Reasons.OfType<IError>()];

    public IReadOnlyList<ISuccess> Successes => _successes ??= [.. Reasons.OfType<ISuccess>()];

    public List<IReason> Reasons { get; init; } = [];

    #endregion
}
