// <copyright file="TypeExtractor.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace DKNet.Fw.Extensions.TypeExtractors;

/// <summary>
///     Provides methods for extracting and filtering types from assemblies.
/// </summary>
/// <remarks>
///     This class implements the <see cref="ITypeExtractor" /> interface and provides methods for extracting and filtering
///     types from assemblies.
///     Purpose: To provide a way to extract and filter types from assemblies.
///     Rationale: Simplifies the process of working with types in assemblies, reducing the amount of boilerplate code.
///     Functionality:
///     - Extracts types from assemblies.
///     - Filters types based on various criteria (e.g., abstract, class, enum, generic, interface, nested, public,
///     attribute, instance of).
///     Integration:
///     - Implements the <see cref="ITypeExtractor" /> interface.
///     Best Practices:
///     - Use the appropriate filtering methods based on the criteria you want to use.
///     - Ensure that the assemblies are loaded before attempting to extract types from them.
/// </remarks>
internal class TypeExtractor : ITypeExtractor
{
    #region Fields

    private readonly Assembly[] _assemblies;
    private readonly List<Expression<Func<Type, bool>>> _predicates = [];

    #endregion

    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="TypeExtractor" /> class.
    /// </summary>
    /// <param name="assemblies">The assemblies to extract types from.</param>
    /// <exception cref="ArgumentException">Thrown when the assemblies collection is null or empty.</exception>
    public TypeExtractor(Assembly[] assemblies)
    {
        if (assemblies == null || assemblies.Length == 0)
            throw new ArgumentException("Assemblies collection cannot be null or empty.", nameof(assemblies));

        _assemblies = [.. assemblies.Distinct()];
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public ITypeExtractor Abstract()
    {
        return FilterBy(t => t.IsAbstract);
    }

    /// <inheritdoc />
    public ITypeExtractor Classes()
    {
        return FilterBy(t => t.IsClass);
    }

    /// <inheritdoc />
    public ITypeExtractor Enums()
    {
        return FilterBy(t => t.IsEnum);
    }

    private TypeExtractor FilterBy(Expression<Func<Type, bool>>? predicate)
    {
        if (predicate != null) _predicates.Add(predicate);

        return this;
    }

    /// <inheritdoc />
    public ITypeExtractor Generic()
    {
        return FilterBy(t => t.IsGenericType);
    }

    /// <inheritdoc />
    public IEnumerator<Type> GetEnumerator()
    {
        var query = _assemblies.SelectMany(a => a.GetTypes()).AsQueryable();
        foreach (var predicate in _predicates) query = query.Where(predicate);

        return query.GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public ITypeExtractor HasAttribute<TAttribute>()
        where TAttribute : Attribute
    {
        return FilterBy(t => t.GetCustomAttributes(typeof(TAttribute), false).Length != 0);
    }

    /// <inheritdoc />
    public ITypeExtractor HasAttribute(Type attributeType)
    {
        return FilterBy(t => t.GetCustomAttributes(attributeType, false).Length != 0);
    }

    /// <inheritdoc />
    public ITypeExtractor Interfaces()
    {
        return FilterBy(t => t.IsInterface);
    }

    /// <inheritdoc />
    public ITypeExtractor IsInstanceOf(Type? type)
    {
        return type == null ? this : FilterBy(t => t.IsImplementOf(type));
    }

    /// <inheritdoc />
    public ITypeExtractor IsInstanceOf<T>() => IsInstanceOf(typeof(T));

    /// <inheritdoc />
    public ITypeExtractor IsInstanceOfAny(params Type[] types)
    {
        return FilterBy(t => types.Any(t.IsImplementOf));
    }

    /// <inheritdoc />
    public ITypeExtractor Nested()
    {
        return FilterBy(t => t.IsNested);
    }

    /// <inheritdoc />
    public ITypeExtractor NotAbstract()
    {
        return FilterBy(t => !t.IsAbstract);
    }

    /// <inheritdoc />
    public ITypeExtractor NotClass()
    {
        return FilterBy(t => !t.IsClass);
    }

    /// <inheritdoc />
    public ITypeExtractor NotEnum()
    {
        return FilterBy(t => !t.IsEnum);
    }

    /// <inheritdoc />
    public ITypeExtractor NotGeneric()
    {
        return FilterBy(t => !t.IsGenericType);
    }

    /// <inheritdoc />
    public ITypeExtractor NotInstanceOf(Type? type)
    {
        return type == null ? this : FilterBy(t => !t.IsImplementOf(type));
    }

    /// <inheritdoc />
    public ITypeExtractor NotInstanceOf<T>() => NotInstanceOf(typeof(T));

    /// <inheritdoc />
    public ITypeExtractor NotInterface()
    {
        return FilterBy(t => !t.IsInterface);
    }

    /// <inheritdoc />
    public ITypeExtractor NotNested()
    {
        return FilterBy(t => !t.IsNested);
    }

    /// <inheritdoc />
    public ITypeExtractor NotPublic()
    {
        return FilterBy(t => !t.IsPublic);
    }

    /// <inheritdoc />
    public ITypeExtractor Publics()
    {
        return FilterBy(t => t.IsPublic);
    }

    /// <inheritdoc />
    public ITypeExtractor Where(Expression<Func<Type, bool>>? predicate) => FilterBy(predicate);

    #endregion
}