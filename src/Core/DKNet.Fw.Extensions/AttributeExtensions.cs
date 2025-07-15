using System.Reflection;

// ReSharper disable MemberCanBePrivate.Global
namespace DKNet.Fw.Extensions;

/// <summary>
///     Attributes extensions methods
/// </summary>
public static class AttributeExtensions
{
    /// <summary>
    ///     Determines whether the provided property has the specified attribute.
    /// </summary>
    /// <typeparam name="TAttribute">The type of the attribute to check for.</typeparam>
    /// <param name="this">The <see cref="PropertyInfo" /> to check.</param>
    /// <param name="inherit">A value indicating whether to search the property's inheritance chain to find the attribute.</param>
    /// <returns><c>true</c> if the attribute is found; otherwise, <c>false</c>.</returns>
    public static bool HasAttribute<TAttribute>(this PropertyInfo? @this, bool inherit = true)
        where TAttribute : Attribute =>
        @this?.GetCustomAttribute<TAttribute>(inherit) != null;

    /// <summary>
    ///     Determines whether the provided type has the specified attribute.
    /// </summary>
    /// <typeparam name="TAttribute">The type of the attribute to check for.</typeparam>
    /// <param name="this">The <see cref="Type" /> to check.</param>
    /// <param name="inherit">A value indicating whether to search the type's inheritance chain to find the attribute.</param>
    /// <returns><c>true</c> if the attribute is found; otherwise, <c>false</c>.</returns>
    public static bool HasAttribute<TAttribute>(this Type? @this, bool inherit = true)
        where TAttribute : Attribute =>
        @this?.GetCustomAttribute<TAttribute>(inherit) != null;

    /// <summary>
    ///     Determines whether the provided object has the specified attribute on the specified property.
    /// </summary>
    /// <typeparam name="TAttribute">The type of the attribute to check for.</typeparam>
    /// <param name="this">The object to check.</param>
    /// <param name="propertyName">The name of the property to check.</param>
    /// <param name="inherit">A value indicating whether to search the property's inheritance chain to find the attribute.</param>
    /// <returns><c>true</c> if the attribute is found; otherwise, <c>false</c>.</returns>
    public static bool HasAttributeOnProperty<TAttribute>(this object @this, string propertyName,
        bool inherit = true) where TAttribute : Attribute
    {
        var prop = @this.GetProperty(propertyName);
        return prop?.HasAttribute<TAttribute>(inherit) == true;
    }
}