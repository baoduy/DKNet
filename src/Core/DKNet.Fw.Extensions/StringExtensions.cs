﻿using System.Reflection;

namespace DKNet.Fw.Extensions;

/// <summary>
/// Provides extension methods for string manipulation and type checking.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Extracts the first sequence of numeric characters from the input string.
    /// </summary>
    /// <param name="input">The string to search within.</param>
    /// <returns>A string containing the extracted numeric characters.</returns>
    public static string ExtractDigits(this string input) =>
        new([.. input.Where(c => char.IsDigit(c) || c is '.' or ',' or '-')]);

    /// <summary>
    /// Determines whether the specified string represents a valid number.
    /// </summary>
    /// <param name="input">The string to evaluate.</param>
    /// <returns><c>true</c> if the string is a valid number; otherwise, <c>false</c>.</returns>
    public static bool IsNumber(this string input) =>
        !string.IsNullOrWhiteSpace(input)
        && input.Count(c => c == '.') <= 1 && !input.Contains(",,", StringComparison.OrdinalIgnoreCase) &&
        input.LastIndexOf('-') <= 0 && input.All(c => char.IsDigit(c) || c == '.' || c == ',' || c == '-');

    /// <summary>
    /// Checks if the specified property can store a string or a value type.
    /// </summary>
    /// <param name="propertyInfo">The <see cref="PropertyInfo"/> of the property to check.</param>
    /// <returns><c>true</c> if the property is capable of storing a string or a value type; otherwise, <c>false</c>.</returns>
    public static bool IsStringOrValueType(this PropertyInfo? propertyInfo)
        => propertyInfo?.PropertyType.IsStringOrValueType() == true;

    /// <summary>
    /// Determines whether a given type is a string or a value type, including handling for nullable types.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to evaluate.</param>
    /// <returns><c>true</c> if the type is a string or value type; otherwise, <c>false</c>.</returns>
    public static bool IsStringOrValueType(this Type? type)
    {
        if (type == null) return false;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            type = type.GenericTypeArguments[0];
        }

        return type == typeof(string) || type.IsValueType;
    }
}
