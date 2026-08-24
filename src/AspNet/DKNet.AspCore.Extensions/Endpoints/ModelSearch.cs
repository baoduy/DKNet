// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ModelSearch.cs
// Description: Discovers the text fields of a projection model a free-text search covers, as Dynamic LINQ clauses.

using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace DKNet.AspCore.Extensions.Endpoints;

/// <summary>
///     Discovers which text fields of a projection model a free-text search should cover, and precompiles the
///     Dynamic LINQ clause for each.
/// </summary>
/// <remarks>
///     <para>
///         Fields are discovered from the <b>model</b>, not the entity, because the model is what the endpoint
///         projects to and therefore what the caller can already see — searching a column absent from the
///         projection would make the endpoint an oracle for it. Each candidate is then resolved against the
///         entity in lockstep, so only a field both visible in the projection and reachable in the query
///         survives.
///     </para>
///     <para>
///         Only <see cref="string" /> fields are searched: "contains" has no meaning for a number, a date, or an
///         id, and a caller who wants to match one of those exactly is already served by an explicit filter.
///         A field reached through a collection is wrapped in <c>Any(...)</c>.
///     </para>
///     <para>
///         Matching is left to the database collation rather than lowercasing both sides, which keeps the
///         column out of a function call. It is still a scan — one <c>LIKE '%…%'</c> per field, OR'd together —
///         which is the accepted trade for a single search box over an arbitrary model.
///     </para>
/// </remarks>
internal static class ModelSearch
{
    #region Fields

    /// <summary>
    ///     How many property hops a searchable field may sit behind — <c>Name</c> and <c>Merchant.Name</c> are
    ///     searched, <c>Merchant.Address.City</c> is not. Bounding the walk is what keeps a model whose graph
    ///     loops back on itself from expanding forever, and keeps the generated OR to a sane width.
    /// </summary>
    private const int MaxDepth = 2;

    /// <summary>Discovered clauses per model/entity pair; reflection over a type graph runs once.</summary>
    private static readonly ConcurrentDictionary<(Type Model, Type Entity), string[]> Cache = new();

    #endregion

    #region Methods

    /// <summary>
    ///     Returns a Dynamic LINQ clause per text field shared by a projection model and the entity it projects
    ///     from, each with <c>@0</c> standing in for the search value.
    /// </summary>
    /// <typeparam name="TModel">Projection model whose properties define what may be searched.</typeparam>
    /// <typeparam name="TEntity">Entity the query runs against.</typeparam>
    /// <returns>
    ///     The clauses — for example <c>Name != null &amp;&amp; Name.Contains(@0)</c> — or an empty array when
    ///     the model exposes no text field, in which case no search can match anything.
    /// </returns>
    internal static string[] Clauses<TModel, TEntity>() =>
        Cache.GetOrAdd(
            (typeof(TModel), typeof(TEntity)),
            static key =>
            {
                var clauses = new List<string>();
                Collect(key.Model, key.Entity, prefix: string.Empty, depth: 1, clauses);
                return [.. clauses];
            });

    /// <summary>
    ///     Walks a model and its entity in lockstep, adding a clause for every text field found.
    /// </summary>
    /// <param name="modelType">Model type to enumerate properties from.</param>
    /// <param name="entityType">Entity type the same-named properties must resolve on.</param>
    /// <param name="prefix">Dynamic LINQ path accumulated so far; empty at the root of a lambda.</param>
    /// <param name="depth">Current hop count, starting at 1.</param>
    /// <param name="into">Collects the discovered clauses.</param>
    private static void Collect(Type modelType, Type entityType, string prefix, int depth, List<string> into)
    {
        foreach (var modelProperty in modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // The projection maps by matching name, so the entity must offer the same name to be queryable.
            var entityProperty = entityType.GetProperty(
                modelProperty.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (entityProperty is null) continue;

            var path = prefix.Length == 0 ? entityProperty.Name : $"{prefix}.{entityProperty.Name}";
            var modelLeaf = Nullable.GetUnderlyingType(modelProperty.PropertyType) ?? modelProperty.PropertyType;

            if (modelLeaf == typeof(string))
            {
                // The null guard is redundant in SQL, where NULL LIKE '%x%' is NULL and excludes the row, but
                // it is load-bearing for a provider that evaluates in memory and would dereference the null.
                into.Add($"{path} != null && {path}.Contains(@0)");
                continue;
            }

            if (depth >= MaxDepth || modelLeaf.IsValueType || IsUnsearchable(modelLeaf)) continue;

            var modelElement = ElementTypeOf(modelLeaf);
            if (modelElement is null)
            {
                Collect(modelLeaf, entityProperty.PropertyType, path, depth + 1, into);
                continue;
            }

            var entityElement = ElementTypeOf(
                Nullable.GetUnderlyingType(entityProperty.PropertyType) ?? entityProperty.PropertyType);
            if (entityElement is null) continue;

            // Inside Any(...) the lambda parameter is implicit, so the nested path restarts from empty.
            var nested = new List<string>();
            Collect(modelElement, entityElement, prefix: string.Empty, depth + 1, nested);
            into.AddRange(nested.Select(clause => $"{path}.Any({clause})"));
        }
    }

    /// <summary>
    ///     Determines whether a complex type should be left out of the walk entirely.
    /// </summary>
    /// <remarks>
    ///     A dictionary is excluded on purpose. Its keys are metadata rather than data, its values are usually a
    ///     serialized column no provider can translate an <c>Any</c> over reliably, and a caller wanting one
    ///     entry of it is served by an explicit filter instead. <c>byte[]</c> is excluded for the same reason a
    ///     blob is not text.
    /// </remarks>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true" /> when the type must not be searched or descended into.</returns>
    private static bool IsUnsearchable(Type type) =>
        type == typeof(byte[])
        || typeof(IDictionary).IsAssignableFrom(type)
        // The type itself is tested as well as its interfaces: a property declared as IDictionary<K,V> is an
        // interface, and GetInterfaces() never reports the type it is called on — nor does the generic
        // interface inherit the non-generic IDictionary the way Dictionary<K,V> the class does. Missing this
        // lets a dictionary through as an ordinary collection and emits an Any() no provider can translate.
        || IsDictionary(type)
        || type.GetInterfaces().Any(IsDictionary);

    /// <summary>Determines whether a type is one of the generic dictionary interfaces.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true" /> when the type is a generic dictionary.</returns>
    private static bool IsDictionary(Type type) =>
        type.IsGenericType
        && (type.GetGenericTypeDefinition() == typeof(IDictionary<,>)
            || type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));

    /// <summary>Returns the element type of a collection type, or <see langword="null" /> if it is not one.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns>The element type, or <see langword="null" />.</returns>
    private static Type? ElementTypeOf(Type type)
    {
        if (type == typeof(string)) return null;

        return type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];
    }

    #endregion
}
