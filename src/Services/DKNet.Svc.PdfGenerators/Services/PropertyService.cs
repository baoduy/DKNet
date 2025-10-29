using System.Reflection;

namespace DKNet.Svc.PdfGenerators.Services;

/// <summary>
///     Helper service for properties.
/// </summary>
/// <summary>
///     Provides PropertyService functionality.
/// </summary>
public static class PropertyService
{
    #region Methods

    public static bool TryGetPropertyValue<TContainer>(string propertyName, out object propertyValue)
        => TryGetPropertyValue<TContainer, object>(propertyName, out propertyValue);

    /// <summary>
    ///     Gets a static property from a type by name.
    /// </summary>
    /// <typeparam name="TContainer">Type that contains the static property.</typeparam>
    /// <typeparam name="TProperty">Type of the property value.</typeparam>
    /// <param name="propertyName">The name of the property to get.</param>
    /// <param name="propertyValue">The property value.</param>
    /// <returns>Whether the conversion was successful.</returns>
    public static bool TryGetPropertyValue<TContainer, TProperty>(string propertyName, out TProperty propertyValue)
    {
        propertyValue = default!;
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        var property = typeof(TContainer).GetProperty(
            propertyName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy);

        object? value;
        Type? memberType;
        if (property != null)
        {
            value = property.GetValue(null, null);
            memberType = property.PropertyType;
        }
        else
        {
            var field = typeof(TContainer).GetField(
                propertyName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy);
            if (field == null)
            {
                return false;
            }

            value = field.GetValue(null);
            memberType = field.FieldType;
        }

        if (value == null)
        {
            propertyValue = default!;
            return true;
        }

        var targetType = typeof(TProperty);

        // Handle nullable types
        if (Nullable.GetUnderlyingType(targetType) != null)
        {
            var underlyingType = Nullable.GetUnderlyingType(targetType);
            if (underlyingType is not null && underlyingType != memberType &&
                !underlyingType.IsAssignableFrom(memberType))
            {
                throw new InvalidCastException($"Member '{propertyName}' is not of type {targetType.Name}.");
            }
        }
        else if (!targetType.IsAssignableFrom(memberType) && !memberType.IsAssignableFrom(targetType))
        {
            throw new InvalidCastException($"Member '{propertyName}' is not of type {targetType.Name}.");
        }

        propertyValue = (TProperty)value;
        return true;
    }

    #endregion
}