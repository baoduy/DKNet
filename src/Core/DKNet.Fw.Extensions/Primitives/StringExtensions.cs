// <copyright file="StringExtensions.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Buffers;
using System.Reflection;

namespace DKNet.Fw.Extensions.Primitives;

/// <summary>
///     Provides extension methods for string manipulation and type checking.
/// </summary>
public static class StringExtensions
{
    #region Methods

    /// <summary>
    ///     Checks if the specified property can store a string or a value type.
    /// </summary>
    /// <param name="propertyInfo">The <see cref="PropertyInfo" /> of the property to check.</param>
    /// <returns><c>true</c> if the property is capable of storing a string or a value type; otherwise, <c>false</c>.</returns>
    public static bool IsStringOrValueType(this PropertyInfo? propertyInfo) =>
        propertyInfo?.PropertyType.IsStringOrValueType() == true;

    /// <summary>
    ///     Determines whether a given type is a string or a value type, including handling for nullable types.
    /// </summary>
    /// <param name="type">The <see cref="Type" /> to evaluate.</param>
    /// <returns><c>true</c> if the type is a string or value type; otherwise, <c>false</c>.</returns>
    public static bool IsStringOrValueType(this Type? type)
    {
        if (type == null) return false;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            type = type.GenericTypeArguments[0];

        return type == typeof(string) || type.IsValueType;
    }

    #endregion

    /// <param name="input">The string to search within.</param>
    extension(string input)
    {
        /// <summary>
        ///     Extracts the first sequence of numeric characters from the input string.
        /// </summary>
        /// <returns>A string containing the extracted numeric characters.</returns>
        public string ExtractDigits()
        {
            ArgumentNullException.ThrowIfNull(input);

            var buffer = ArrayPool<char>.Shared.Rent(input.Length);
            try
            {
                var count = 0;
                foreach (var c in input)
                    if (char.IsDigit(c) || c is '.' or ',' or '-')
                        buffer[count++] = c;

                return new string(buffer, 0, count);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }

        /// <summary>
        ///     Determines whether the specified string represents a valid number.
        /// </summary>
        /// <returns><c>true</c> if the string is a valid number; otherwise, <c>false</c>.</returns>
        public bool IsNumber()
        {
            if (string.IsNullOrWhiteSpace(input)) return false;

            var dotCount = 0;
            var lastDashIndex = -1;
            for (var i = 0; i < input.Length; i++)
            {
                var c = input[i];
                switch (c)
                {
                    case '.':
                        dotCount++;
                        break;
                    case ',':
                        if (i > 0 && input[i - 1] == ',') return false;
                        break;
                    case '-':
                        lastDashIndex = i;
                        break;
                    default:
                        if (!char.IsDigit(c)) return false;
                        break;
                }
            }

            return dotCount <= 1 && lastDashIndex <= 0;
        }
    }
}