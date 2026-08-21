// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: IContextualSource.cs
// Description: Marker abstraction for a request member populated from a contextual source rather than the caller.

namespace DKNet.AspCore.Extensions.ModelBinding;

/// <summary>
///     Marks an attribute as declaring that the property it decorates is populated from a contextual source —
///     the authenticated caller's claims today, potentially other sources later — before the request reaches
///     validation and its handler. The population mechanism keys off this abstraction rather than any concrete
///     attribute, so a new source kind needs only a new attribute implementing this interface plus a matching
///     <see cref="IContextualValueResolver" />; the mechanism itself never changes.
/// </summary>
public interface IContextualSource
{
}
