namespace DKNet.Fw.Extensions;

/// <summary>
///     Provides extension methods for the <see cref="Type" /> class, enabling type-related operations such as checking
///     type implementations, inheritance, and numeric type validation.
/// </summary>
public static class TypeExtensions
{
    public static bool IsAssignableFrom<TType>(this Type type) => type.IsAssignableFrom(typeof(TType));
    public static bool IsAssignableTo<TType>(this Type type) => type.IsAssignableTo(typeof(TType));

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
}