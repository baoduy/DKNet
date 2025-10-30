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
        {
            throw new ArgumentException("Assemblies collection cannot be null or empty.", nameof(assemblies));
        }

        this._assemblies = [.. assemblies.Distinct()];
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public ITypeExtractor Abstract()
    {
        return this.FilterBy(t => t.IsAbstract);
    }

    /// <inheritdoc />
    public ITypeExtractor Classes()
    {
        return this.FilterBy(t => t.IsClass);
    }

    /// <inheritdoc />
    public ITypeExtractor Enums()
    {
        return this.FilterBy(t => t.IsEnum);
    }

    private TypeExtractor FilterBy(Expression<Func<Type, bool>>? predicate)
    {
        if (predicate != null)
        {
            this._predicates.Add(predicate);
        }

        return this;
    }

    /// <inheritdoc />
    public ITypeExtractor Generic()
    {
        return this.FilterBy(t => t.IsGenericType);
    }

    /// <inheritdoc />
    public IEnumerator<Type> GetEnumerator()
    {
        var query = this._assemblies.SelectMany(a => a.GetTypes()).AsQueryable();
        foreach (var predicate in this._predicates)
        {
            query = query.Where(predicate);
        }

        return query.GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    /// <inheritdoc />
    public ITypeExtractor HasAttribute<TAttribute>()
        where TAttribute : Attribute
    {
        return this.FilterBy(t => t.GetCustomAttributes(typeof(TAttribute), false).Length != 0);
    }

    /// <inheritdoc />
    public ITypeExtractor HasAttribute(Type attributeType)
    {
        return this.FilterBy(t => t.GetCustomAttributes(attributeType, false).Length != 0);
    }

    /// <inheritdoc />
    public ITypeExtractor Interfaces()
    {
        return this.FilterBy(t => t.IsInterface);
    }

    /// <inheritdoc />
    public ITypeExtractor IsInstanceOf(Type? type)
    {
        return type == null ? this : this.FilterBy(t => t.IsImplementOf(type));
    }

    /// <inheritdoc />
    public ITypeExtractor IsInstanceOf<T>() => this.IsInstanceOf(typeof(T));

    /// <inheritdoc />
    public ITypeExtractor IsInstanceOfAny(params Type[] types)
    {
        return this.FilterBy(t => types.Any(t.IsImplementOf));
    }

    /// <inheritdoc />
    public ITypeExtractor Nested()
    {
        return this.FilterBy(t => t.IsNested);
    }

    /// <inheritdoc />
    public ITypeExtractor NotAbstract()
    {
        return this.FilterBy(t => !t.IsAbstract);
    }

    /// <inheritdoc />
    public ITypeExtractor NotClass()
    {
        return this.FilterBy(t => !t.IsClass);
    }

    /// <inheritdoc />
    public ITypeExtractor NotEnum()
    {
        return this.FilterBy(t => !t.IsEnum);
    }

    /// <inheritdoc />
    public ITypeExtractor NotGeneric()
    {
        return this.FilterBy(t => !t.IsGenericType);
    }

    /// <inheritdoc />
    public ITypeExtractor NotInstanceOf(Type? type)
    {
        return type == null ? this : this.FilterBy(t => !t.IsImplementOf(type));
    }

    /// <inheritdoc />
    public ITypeExtractor NotInstanceOf<T>() => this.NotInstanceOf(typeof(T));

    /// <inheritdoc />
    public ITypeExtractor NotInterface()
    {
        return this.FilterBy(t => !t.IsInterface);
    }

    /// <inheritdoc />
    public ITypeExtractor NotNested()
    {
        return this.FilterBy(t => !t.IsNested);
    }

    /// <inheritdoc />
    public ITypeExtractor NotPublic()
    {
        return this.FilterBy(t => !t.IsPublic);
    }

    /// <inheritdoc />
    public ITypeExtractor Publics()
    {
        return this.FilterBy(t => t.IsPublic);
    }

    /// <inheritdoc />
    public ITypeExtractor Where(Expression<Func<Type, bool>>? predicate) => this.FilterBy(predicate);

    #endregion
}