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
    public static bool IsNullableType(this Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        // Check if the type is a generic type and is Nullable<>
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    /// <summary>
    ///     Gets a property by name, considering all access levels and ignoring case.
    /// </summary>
    /// <typeparam name="T">The type of the object to get the property from.</typeparam>
    /// <param name="obj">The object to get the property from.</param>
    /// <param name="propertyName">The name of the property to get.</param>
    /// <param name="flags"></param>
    /// <returns>The <see cref="PropertyInfo" /> of the property, or null if not found.</returns>
    [UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2075",
        Justification = "Everything referenced in the loaded assembly is manually preserved, so it's safe")]
    public static PropertyInfo? GetProperty<T>(this T? obj, string propertyName,
        BindingFlags flags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic |
                             BindingFlags.Instance) where T : class
    {
        if (obj == null || string.IsNullOrEmpty(propertyName)) return null;

        var type = obj as Type ?? obj.GetType();
        return type.GetProperty(propertyName, flags);
    }

    /// <summary>
    ///     Gets the value of a property by name, considering all access levels and ignoring case.
    ///     Supports nested properties.
    /// </summary>
    /// <typeparam name="T">The type of the object to get the property value from.</typeparam>
    /// <param name="obj">The object to get the property value from.</param>
    /// <param name="propertyName">The name of the property to get.</param>
    /// <returns>The value of the property, or null if not found or if the object is null.</returns>
    public static object? GetPropertyValue<T>(this T? obj, string propertyName) where T : class
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

    /// <summary>
    ///     Sets the value of a property.
    /// </summary>
    /// <param name="obj">The object to set the property on.</param>
    /// <param name="property">The <see cref="PropertyInfo" /> of the property to set.</param>
    /// <param name="value">The value to set.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when the <paramref name="obj" /> or <paramref name="property" /> is
    ///     null.
    /// </exception>
    public static void SetPropertyValue(this object obj, PropertyInfo property, object? value)
    {
        ArgumentNullException.ThrowIfNull(obj);
        ArgumentNullException.ThrowIfNull(property);

        if (value == null)
        {
            property.SetValue(obj, null);
        }
        else if (property.PropertyType.IsNullableType())
        {
            property.SetValue(obj,
                Convert.ChangeType(value, Nullable.GetUnderlyingType(property.PropertyType)!,
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
    /// <param name="obj">The object to set the property on.</param>
    /// <param name="propertyName">The name of the property to set.</param>
    /// <param name="value">The value to set.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when the <paramref name="obj" /> or <paramref name="propertyName" /> is
    ///     null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when the property is not found.</exception>
    public static void SetPropertyValue(this object obj, string propertyName, object value)
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
    /// <param name="obj">The object to set the property on.</param>
    /// <param name="propertyName">The name of the property to set.</param>
    /// <param name="value">The value to set.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when the <paramref name="obj" /> or <paramref name="propertyName" /> is
    ///     null.
    /// </exception>
    public static void TrySetPropertyValue(this object obj, string propertyName, object value)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj), "The target object cannot be null.");

        if (string.IsNullOrEmpty(propertyName))
            throw new ArgumentNullException(nameof(propertyName), "The property name cannot be null or empty.");

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
    /// <param name="obj">The object to set the property on.</param>
    /// <param name="property">The <see cref="PropertyInfo" /> of the property to set.</param>
    /// <param name="value">The value to set.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when the <paramref name="obj" /> or <paramref name="property" /> is
    ///     null.
    /// </exception>
    public static void TrySetPropertyValue(this object obj, PropertyInfo property, object? value)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj), "The target object cannot be null.");

        if (property == null) throw new ArgumentNullException(nameof(property), "The property info cannot be null.");

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