using System.ComponentModel.DataAnnotations;
using System.Reflection;

// ReSharper disable MemberCanBePrivate.Global

namespace DKNet.Fw.Extensions;

/// <summary>
///     Helper class for working with enums in WPF and other .NET applications.
/// </summary>
public static class EnumExtensions
{
    #region Methods

    /// <summary>
    ///     Gets the display attribute of the provided enum.
    /// </summary>
    /// <typeparam name="T">The type of the attribute to retrieve.</typeparam>
    /// <param name="this">The enum to get the attribute from.</param>
    /// <returns>The attribute of type T, or null if not found.</returns>
    public static T? GetAttribute<T>(this Enum? @this) where T : Attribute
    {
        if (@this is null) return null;

        var type = @this.GetType();
        var f = type.GetField(@this.ToString());
        return f?.GetCustomAttribute<T>();
    }

    /// <summary>
    ///     Gets the enum information, including description, name, and group name, from the display attribute of the provided
    ///     enum.
    /// </summary>
    /// <param name="this">The enum to get the information from.</param>
    /// <returns>The <see cref="EnumInfo" /> containing the enum's information, or null if the enum is null.</returns>
    public static EnumInfo? GetEumInfo(this Enum? @this)
    {
        if (@this == null) return null;

        var att = @this.GetAttribute<DisplayAttribute>();

        return new EnumInfo
        {
            Key = @this.ToString(),
            Description = att?.Description!,
            Name = att?.Name!,
            GroupName = att?.GroupName!
        };
    }

    /// <summary>
    ///     Gets the enum information for all values of the specified enum type.
    /// </summary>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <returns>An <see cref="IEnumerable{EnumInfo}" /> containing the enum's information.</returns>
    public static IEnumerable<EnumInfo> GetEumInfos<T>() where T : Enum
    {
        var type = typeof(T);
        var members = type.GetFields();

        foreach (var info in members)
        {
            if (info.FieldType == typeof(int)) continue;

            var att = info.GetCustomAttribute<DisplayAttribute>();

            yield return new EnumInfo
            {
                Key = info.Name,
                Description = att?.Description!,
                Name = att?.Name ?? info.Name,
                GroupName = att?.GroupName!
            };
        }
    }

    #endregion
}