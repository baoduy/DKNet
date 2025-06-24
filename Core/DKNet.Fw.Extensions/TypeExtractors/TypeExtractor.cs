using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace DKNet.Fw.Extensions.TypeExtractors;

/// <summary>
/// Provides methods for extracting and filtering types from assemblies.
/// </summary>
/// <remarks>
/// This class implements the <see cref="ITypeExtractor"/> interface and provides methods for extracting and filtering types from assemblies.
/// 
/// Purpose: To provide a way to extract and filter types from assemblies.
/// Rationale: Simplifies the process of working with types in assemblies, reducing the amount of boilerplate code.
/// 
/// Functionality:
/// - Extracts types from assemblies.
/// - Filters types based on various criteria (e.g., abstract, class, enum, generic, interface, nested, public, attribute, instance of).
/// 
/// Integration:
/// - Implements the <see cref="ITypeExtractor"/> interface.
/// 
/// Best Practices:
/// - Use the appropriate filtering methods based on the criteria you want to use.
/// - Ensure that the assemblies are loaded before attempting to extract types from them.
/// </remarks>
internal class TypeExtractor : ITypeExtractor
{
    private readonly Assembly[] _assemblies;
    private readonly List<Expression<Func<Type, bool>>> _predicates = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeExtractor"/> class.
    /// </summary>
    /// <param name="assemblies">The assemblies to extract types from.</param>
    /// <exception cref="ArgumentException">Thrown when the assemblies collection is null or empty.</exception>
    public TypeExtractor(Assembly[] assemblies)
    {
        if (assemblies == null || assemblies.Length == 0)
            throw new ArgumentException("Assemblies collection cannot be null or empty.", nameof(assemblies));

        _assemblies = [.. assemblies.Distinct()];
    }

    /// <inheritdoc />
    public ITypeExtractor Abstract() => FilterBy(t => t.IsAbstract);

    /// <inheritdoc />
    public ITypeExtractor Classes() => FilterBy(t => t.IsClass);

    /// <inheritdoc />
    public ITypeExtractor Enums() => FilterBy(t => t.IsEnum);

    /// <inheritdoc />
    public ITypeExtractor Generic() => FilterBy(t => t.IsGenericType);

    /// <inheritdoc />
    public ITypeExtractor HasAttribute<TAttribute>() where TAttribute : Attribute
        => FilterBy(t => t.GetCustomAttributes(typeof(TAttribute), false).Length != 0);

    /// <inheritdoc />
    public ITypeExtractor HasAttribute(Type attributeType)
        => FilterBy(t => t.GetCustomAttributes(attributeType, false).Length != 0);

    /// <inheritdoc />
    public ITypeExtractor Interfaces() => FilterBy(t => t.IsInterface);

    /// <inheritdoc />
    public ITypeExtractor IsInstanceOf(Type? type)
        => type == null ? this : FilterBy(t => t.IsImplementOf(type));

    /// <inheritdoc />
    public ITypeExtractor IsInstanceOf<T>() => IsInstanceOf(typeof(T));

    /// <inheritdoc />
    public ITypeExtractor IsInstanceOfAny(params Type[] types)
        => FilterBy(t => types.Any(t.IsImplementOf));

    /// <inheritdoc />
    public ITypeExtractor Nested() => FilterBy(t => t.IsNested);

    /// <inheritdoc />
    public ITypeExtractor NotAbstract() => FilterBy(t => !t.IsAbstract);

    /// <inheritdoc />
    public ITypeExtractor NotClass() => FilterBy(t => !t.IsClass);

    /// <inheritdoc />
    public ITypeExtractor NotEnum() => FilterBy(t => !t.IsEnum);

    /// <inheritdoc />
    public ITypeExtractor NotGeneric() => FilterBy(t => !t.IsGenericType);

    /// <inheritdoc />
    public ITypeExtractor NotInstanceOf(Type? type)
        => type == null ? this : FilterBy(t => !t.IsImplementOf(type));

    /// <inheritdoc />
    public ITypeExtractor NotInstanceOf<T>() => NotInstanceOf(typeof(T));

    /// <inheritdoc />
    public ITypeExtractor NotInterface() => FilterBy(t => !t.IsInterface);

    /// <inheritdoc />
    public ITypeExtractor NotNested() => FilterBy(t => !t.IsNested);

    /// <inheritdoc />
    public ITypeExtractor NotPublic() => FilterBy(t => !t.IsPublic);

    /// <inheritdoc />
    public ITypeExtractor Publics() => FilterBy(t => t.IsPublic);

    /// <inheritdoc />
    public ITypeExtractor Where(Expression<Func<Type, bool>>? predicate)
        => FilterBy(predicate);

    /// <inheritdoc />
    public IEnumerator<Type> GetEnumerator()
    {
        var query = _assemblies.SelectMany(a => a.GetTypes()).AsQueryable();
        foreach (var predicate in _predicates)
            query = query.Where(predicate);
        return query.GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private TypeExtractor FilterBy(Expression<Func<Type, bool>>? predicate)
    {
        if (predicate != null)
            _predicates.Add(predicate);
        return this;
    }
}