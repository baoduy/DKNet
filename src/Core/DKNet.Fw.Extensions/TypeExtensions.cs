// <copyright file="TypeExtensions.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace DKNet.Fw.Extensions;

/// <summary>
///     Provides extension methods for the <see cref="Type" /> class, enabling type-related operations such as checking
///     type implementations, inheritance, and numeric type validation.
/// </summary>
public static class TypeExtensions
{
    #region Methods

    /// <summary>
    ///     Determines whether an instance of the specified type <typeparamref name="TType" /> can be assigned to a variable of
    ///     the current type.
    /// </summary>
    /// <typeparam name="TType">The type to compare with the current type.</typeparam>
    /// <param name="type">The current type.</param>
    /// <returns>
    ///     <c>true</c> if an instance of <typeparamref name="TType" /> can be assigned to a variable of the current type;
    ///     otherwise, <c>false</c>.
    /// </returns>
    public static bool IsAssignableFrom<TType>(this Type type) => type.IsAssignableFrom(typeof(TType));

    /// <summary>
    ///     Determines whether the current type can be assigned to a variable of the specified type
    ///     <typeparamref name="TType" />.
    /// </summary>
    /// <typeparam name="TType">The type to compare with the current type.</typeparam>
    /// <param name="type">The current type.</param>
    /// <returns>
    ///     <c>true</c> if the current type can be assigned to a variable of <typeparamref name="TType" />; otherwise,
    ///     <c>false</c>.
    /// </returns>
    public static bool IsAssignableTo<TType>(this Type type) => type.IsAssignableTo(typeof(TType));

    /// <summary>
    ///     Determines whether the specified type implements or inherits from the given matching type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <param name="matching">The type to compare against.</param>
    /// <returns>
    ///     <c>true</c> if the specified type implements or inherits from the matching type; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsImplementOf(this Type? type, Type? matching)
    {
        if (type == null || matching == null)
        {
            return false;
        }

        if (type == matching)
        {
            return false;
        }

        if (matching.IsAssignableFrom(type))
        {
            return true;
        }

        if (matching.IsInterface)
        {
            return type.GetInterfaces().Any(y =>
                (y.IsGenericType && y.GetGenericTypeDefinition() == matching) || matching.IsAssignableFrom(y));
        }

        while (type != null)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == matching)
            {
                return true;
            }

            type = type.BaseType;
        }

        return false;
    }

    /// <summary>
    ///     Determines whether the specified type implements or inherits from the given generic type <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The type to compare against.</typeparam>
    /// <param name="type">The type to check.</param>
    /// <returns>
    ///     <c>true</c> if the specified type implements or inherits from the type <typeparamref name="T" />; otherwise,
    ///     <c>false</c>.
    /// </returns>
    public static bool IsImplementOf<T>(this Type type) => type.IsImplementOf(typeof(T));

    /// <summary>
    ///     Determines whether the specified type is a numeric type.
    /// </summary>
    /// <param name="this">The type to check.</param>
    /// <returns>
    ///     <c>true</c> if the type is a numeric type; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    ///     Numeric types include <see cref="byte" />, <see cref="sbyte" />, <see cref="ushort" />, <see cref="uint" />,
    ///     <see cref="ulong" />, <see cref="short" />, <see cref="int" />, <see cref="long" />, <see cref="decimal" />,
    ///     <see cref="double" />, and <see cref="float" />.
    /// </remarks>
    public static bool IsNumericType(this Type @this)
    {
        ArgumentNullException.ThrowIfNull(@this);

        var t = @this.IsNullableType() ? Nullable.GetUnderlyingType(@this)! : @this;
        return Type.GetTypeCode(t) switch
        {
            TypeCode.Byte or TypeCode.SByte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 or TypeCode.Int16
                or TypeCode.Int32 or TypeCode.Int64 or TypeCode.Decimal or TypeCode.Double or TypeCode.Single => true,
            _ => false
        };
    }

    /// <summary>
    ///     Determines whether the specified object is of a numeric type.
    /// </summary>
    /// <param name="this">The object to check.</param>
    /// <returns>
    ///     <c>true</c> if the object is of a numeric type; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsNumericType(this object? @this) => @this?.GetType().IsNumericType() ?? false;

    #endregion
}