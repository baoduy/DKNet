using System.Reflection;

namespace DKNet.Fw.Extensions.TypeExtractors;

/// <summary>
///     Provides extension methods for extracting types from assemblies.
/// </summary>
/// <remarks>
///     This class simplifies the process of extracting types from assemblies.
///     Purpose: To provide a set of extension methods for extracting types from assemblies.
///     Rationale: Simplifies the process of extracting types from assemblies, reducing the amount of boilerplate code.
///     Functionality:
///     - Extracts types from a single assembly.
///     - Extracts types from an array of assemblies.
///     - Extracts types from a collection of assemblies.
///     Integration:
///     - Can be used as an extension method for Assembly, Assembly[], and ICollection&lt;Assembly&gt;.
///     Best Practices:
///     - Use the appropriate extension method based on the type of input.
///     - Ensure that the assemblies are loaded before attempting to extract types from them.
/// </remarks>
public static class TypeArrayExtractorExtensions
{
    #region Methods

    /// <summary>
    ///     Extracts types from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to extract types from.</param>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    public static ITypeExtractor Extract(this Assembly assembly)
    {
        return new[] { assembly }.Extract();
    }

    /// <summary>
    ///     Extracts types from the specified array of assemblies.
    /// </summary>
    /// <param name="assemblies">The array of assemblies to extract types from.</param>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    public static ITypeExtractor Extract(this Assembly[] assemblies) => new TypeExtractor(assemblies);

    /// <summary>
    ///     Extracts types from the specified collection of assemblies.
    /// </summary>
    /// <param name="assemblies">The collection of assemblies to extract types from.</param>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    public static ITypeExtractor Extract(this ICollection<Assembly> assemblies) => new TypeExtractor([.. assemblies]);

    #endregion
}