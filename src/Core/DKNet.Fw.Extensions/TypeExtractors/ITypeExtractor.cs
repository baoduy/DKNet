using System.Linq.Expressions;

namespace DKNet.Fw.Extensions.TypeExtractors;

/// <summary>
///     Interface for extracting types with various filtering options.
/// </summary>
/// <remarks>
///     This interface defines the methods for extracting types from assemblies and filtering them based on various
///     criteria.
///     Purpose: To provide a way to extract and filter types from assemblies.
///     Rationale: Simplifies the process of working with types in assemblies, reducing the amount of boilerplate code.
///     Functionality:
///     - Filters types based on various criteria (e.g., abstract, class, enum, generic, interface, nested, public,
///     attribute, instance of).
///     Integration:
///     - Implemented by the <see cref="TypeExtractor" /> class.
///     Best Practices:
///     - Use the appropriate filtering methods based on the criteria you want to use.
/// </remarks>
public interface ITypeExtractor : IEnumerable<Type>
{
    /// <summary>
    ///     Filters the types to include only abstract classes.
    /// </summary>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor Abstract();

    /// <summary>
    ///     Filters the types to include only classes.
    /// </summary>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor Classes();

    /// <summary>
    ///     Filters the types to include only enums.
    /// </summary>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor Enums();

    /// <summary>
    ///     Filters the types to include only generic types.
    /// </summary>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor Generic();

    /// <summary>
    ///     Filters the types to include only those with the specified attribute.
    /// </summary>
    /// <typeparam name="TAttribute">The type of the attribute to filter by.</typeparam>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor HasAttribute<TAttribute>() where TAttribute : Attribute;

    /// <summary>
    ///     Filters the types to include only those with the specified attribute.
    /// </summary>
    /// <param name="attributeType">The type of the attribute to filter by.</param>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor HasAttribute(Type attributeType);

    /// <summary>
    ///     Filters the types to include only interfaces.
    /// </summary>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor Interfaces();

    /// <summary>
    ///     Filters the types to include only those that are instances of the specified type.
    /// </summary>
    /// <param name="type">The type to filter by.</param>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor IsInstanceOf(Type? type);

    /// <summary>
    ///     Filters the types to include only those that are instances of the specified type.
    /// </summary>
    /// <typeparam name="T">The type to filter by.</typeparam>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor IsInstanceOf<T>();

    /// <summary>
    ///     Filters the types to include only those that are instances of any of the specified types.
    /// </summary>
    /// <param name="types">The types to filter by.</param>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor IsInstanceOfAny(params Type[] types);

    /// <summary>
    ///     Filters the types to include only nested types.
    /// </summary>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor Nested();

    /// <summary>
    ///     Filters the types to exclude abstract classes.
    /// </summary>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor NotAbstract();

    /// <summary>
    ///     Filters the types to exclude classes.
    /// </summary>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor NotClass();

    /// <summary>
    ///     Filters the types to exclude enums.
    /// </summary>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor NotEnum();

    /// <summary>
    ///     Filters the types to exclude generic types.
    /// </summary>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor NotGeneric();

    /// <summary>
    ///     Filters the types to exclude those that are instances of the specified type.
    /// </summary>
    /// <param name="type">The type to filter by.</param>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor NotInstanceOf(Type? type);

    /// <summary>
    ///     Filters the types to exclude those that are instances of the specified type.
    /// </summary>
    /// <typeparam name="T">The type to filter by.</typeparam>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor NotInstanceOf<T>();

    /// <summary>
    ///     Filters the types to exclude interfaces.
    /// </summary>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor NotInterface();

    /// <summary>
    ///     Filters the types to exclude nested types.
    /// </summary>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor NotNested();

    /// <summary>
    ///     Filters the types to exclude public types.
    /// </summary>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor NotPublic();

    /// <summary>
    ///     Filters the types to include only public types.
    /// </summary>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor Publics();

    /// <summary>
    ///     Filters the types using the specified predicate.
    /// </summary>
    /// <param name="predicate">The predicate to filter by.</param>
    /// <returns>An <see cref="ITypeExtractor" /> instance for further filtering.</returns>
    ITypeExtractor Where(Expression<Func<Type, bool>>? predicate);
}