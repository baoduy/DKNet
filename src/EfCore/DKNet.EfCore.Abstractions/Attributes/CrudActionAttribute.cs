// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DKNet.EfCore.Abstractions.Attributes;

/// <summary>
///     Marks a public method as a domain-action HTTP endpoint for CRUD vertical-slice generation.
/// </summary>
/// <param name="route">
///     The route segment appended for this action. When not set, the generator derives a kebab-case
///     default from the method name.
/// </param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class CrudActionAttribute(string? route = null) : Attribute
{
    /// <summary>
    ///     The route segment appended for this action, or <see langword="null" /> when unset.
    /// </summary>
    public string? Route { get; } = route;

    /// <summary>
    ///     The HTTP verb registered for this action. Defaults to <see cref="CrudActionVerb.Post" />.
    /// </summary>
    public CrudActionVerb Verb { get; set; } = CrudActionVerb.Post;

    /// <summary>
    ///     Overrides the generated request type name. When not set, the generator derives the name
    ///     from the containing entity and method.
    /// </summary>
    public string? Name { get; set; }
}
