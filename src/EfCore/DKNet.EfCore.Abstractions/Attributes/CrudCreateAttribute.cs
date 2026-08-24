// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DKNet.EfCore.Abstractions.Attributes;

/// <summary>
///     Marks the constructor or method whose parameters become the generated Create request for CRUD
///     vertical-slice generation.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method, AllowMultiple = false)]
public sealed class CrudCreateAttribute : Attribute
{
    /// <summary>
    ///     Overrides the generated Create request type name. When not set, the generator derives the name
    ///     from the containing entity.
    /// </summary>
    public string? Name { get; set; }
}
