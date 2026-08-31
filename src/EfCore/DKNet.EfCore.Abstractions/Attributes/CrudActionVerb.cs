// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DKNet.EfCore.Abstractions.Attributes;

/// <summary>
///     The HTTP verb a generated domain-action endpoint registers.
/// </summary>
public enum CrudActionVerb
{
    /// <summary>Registers the action as an HTTP POST endpoint. The default verb.</summary>
    Post,

    /// <summary>Registers the action as an HTTP PUT endpoint.</summary>
    Put,

    /// <summary>Registers the action as an HTTP PATCH endpoint.</summary>
    Patch
}
