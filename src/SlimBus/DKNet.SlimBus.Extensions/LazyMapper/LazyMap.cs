// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: LazyMap.cs
// Description: Lazily maps a source value to a target type via Mapster, deferring the mapping until Value or ValueOrDefault is read.

using MapsterMapper;

namespace DKNet.SlimBus.Extensions.LazyMapper;

/// <summary>
///     Represents a value that is lazily mapped to <typeparamref name="TResult" /> on first access.
/// </summary>
/// <typeparam name="TResult">The target type the underlying value is mapped to.</typeparam>
public interface ILazyMap<out TResult>
{
    #region Properties

    /// <summary>
    ///     Gets the mapped value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the underlying original value is <c>null</c>.</exception>
    TResult Value { get; }

    /// <summary>
    ///     Gets the mapped value, or the default value of <typeparamref name="TResult" /> when the underlying
    ///     original value is <c>null</c>.
    /// </summary>
    TResult? ValueOrDefault { get; }

    #endregion
}

internal class LazyMap<TResult>(object? originalValue, IMapper mapper) : ILazyMap<TResult>
{
    #region Fields

    private readonly IMapper _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    private TResult? _value;

    #endregion

    #region Properties

    public TResult Value => ValueOrDefault ?? throw new InvalidOperationException(nameof(ValueOrDefault));

    public TResult ValueOrDefault => GetValue()!;

    #endregion

    #region Methods

    private TResult? GetValue()
    {
        if (originalValue is null)
        {
            return default;
        }

        if (_value is not null)
        {
            return _value;
        }

        if (originalValue is TResult o)
        {
            _value = o;
        }
        else
        {
            _value = _mapper.Map<TResult>(originalValue);
        }

        return _value;
    }

    #endregion
}
