// <copyright file="PropertyExtensions.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace DKNet.Fw.Extensions;

/// <summary>
///     The extensions methods to work with object properties.
/// </summary>
public static class PropertyExtensions
{
    #region Methods

    /// <summary>
    ///     Determines whether the specified type is a nullable value type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns><c>true</c> if the type is a nullable value type (Nullable&lt;T&gt;); otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type" /> is null.</exception>
    public static bool IsNullableType(this Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        // Check if the type is a generic type and is Nullable<>
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    #endregion

    /// <param name="obj">The object to get the property from.</param>
    /// <typeparam name="T">The type of the object to get the property from.</typeparam>
    extension<T>(T? obj) where T : class
    {
        /// <summary>
        ///     Gets a property by name, considering all access levels and ignoring case.
        /// </summary>
        /// <param name="propertyName">The name of the property to get.</param>
        /// <param name="flags">The attribute flag.</param>
        /// <returns>The <see cref="PropertyInfo" /> of the property, or null if not found.</returns>
        [UnconditionalSuppressMessage(
            "AssemblyLoadTrimming",
            "IL2075",
            Justification = "Everything referenced in the loaded assembly is manually preserved, so it's safe")]
        public PropertyInfo? GetProperty(string propertyName,
            BindingFlags flags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic |
                                 BindingFlags.Instance)
        {
            if (obj == null || string.IsNullOrEmpty(propertyName)) return null;

            var type = obj as Type ?? obj.GetType();
            return type.GetProperty(propertyName, flags);
        }

        /// <summary>
        ///     Gets the value of a property by name, considering all access levels and ignoring case.
        ///     Supports nested properties.
        /// </summary>
        /// <param name="propertyName">The name of the property to get.</param>
        /// <returns>The value of the property, or null if not found or if the object is null.</returns>
        public object? GetPropertyValue(string propertyName)
        {
            if (obj == null || string.IsNullOrEmpty(propertyName)) return null;

            var properties = propertyName.Split('.');
            object? current = obj;

            foreach (var prop in properties)
            {
                if (current == null) break;

                var propertyInfo = current.GetProperty(prop);
                if (propertyInfo == null) return null;

                current = propertyInfo.GetValue(current);
            }

            return current;
        }
    }

    /// <param name="obj">The object to set the property on.</param>
    extension(object obj)
    {
        /// <summary>
        ///     Sets the value of a property.
        /// </summary>
        /// <param name="property">The <see cref="PropertyInfo" /> of the property to set.</param>
        /// <param name="value">The value to set.</param>
        /// <exception cref="ArgumentNullException">
        /// </exception>
        public void SetPropertyValue(PropertyInfo property, object? value)
        {
            ArgumentNullException.ThrowIfNull(obj);
            ArgumentNullException.ThrowIfNull(property);

            if (value == null)
            {
                property.SetValue(obj, null);
            }
            else if (property.PropertyType.IsNullableType())
            {
                property.SetValue(
                    obj,
                    Convert.ChangeType(
                        value,
                        Nullable.GetUnderlyingType(property.PropertyType)!,
                        CultureInfo.CurrentCulture));
            }
            else
            {
                value = property.PropertyType.IsEnum
                    ? Enum.Parse(property.PropertyType, value.ToString()!)
                    : Convert.ChangeType(value, property.PropertyType, CultureInfo.CurrentCulture);

                property.SetValue(obj, value);
            }
        }

        /// <summary>
        ///     Sets the value of a property by name, considering all access levels and ignoring case.
        /// </summary>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="value">The value to set.</param>
        /// <exception cref="ArgumentNullException">
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when the property is not found.</exception>
        public void SetPropertyValue(string propertyName, object value)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (string.IsNullOrEmpty(propertyName)) throw new ArgumentNullException(nameof(propertyName));

            var property = obj.GetProperty(propertyName) ??
                           throw new ArgumentException(
                               $"Property '{propertyName}' not found on type '{obj.GetType().FullName}'.",
                               nameof(propertyName));
            obj.SetPropertyValue(property, value);
        }

        /// <summary>
        ///     Tries to set the value of a property by name, considering all access levels and ignoring case, and ignores any
        ///     exceptions that occur.
        /// </summary>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="value">The value to set.</param>
        /// <exception cref="ArgumentNullException">
        /// </exception>
        public void TrySetPropertyValue(string propertyName, object value)
        {
            ArgumentException.ThrowIfNullOrEmpty(propertyName);
            ArgumentNullException.ThrowIfNull(obj);

            try
            {
                // Attempt to set the property value using the SetPropertyValue method
                obj.SetPropertyValue(propertyName, value);
            }
            catch (ArgumentNullException ex)
            {
                Debug.WriteLine($"Failed to set property {propertyName}: {ex.Message}");
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine($"Failed to set property {propertyName}: {ex.Message}");
            }
        }

        /// <summary>
        ///     Tries to set the value of a property and ignores any exceptions that occur.
        /// </summary>
        /// <param name="property">The <see cref="PropertyInfo" /> of the property to set.</param>
        /// <param name="value">The value to set.</param>
        /// <exception cref="ArgumentNullException">
        /// </exception>
        public void TrySetPropertyValue(PropertyInfo property, object? value)
        {
            ArgumentNullException.ThrowIfNull(property);
            ArgumentNullException.ThrowIfNull(obj);

            try
            {
                // Attempt to set the property value using the SetPropertyValue method
                obj.SetPropertyValue(property, value);
            }
            catch (ArgumentNullException ex)
            {
                Debug.WriteLine($"Failed to set property {property.Name}: {ex.Message}");
            }
            catch (FormatException ex)
            {
                Debug.WriteLine($"Failed to set property {property.Name}: {ex.Message}");
            }
        }
    }
}