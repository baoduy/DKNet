// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DKNet.EfCore.Abstractions.Attributes;

/// <summary>
///     The SensitiveData attribute can be applied to an entity property to declare it sensitive. This has two
///     independent meanings: for audit-log redaction purposes, the audit-log redactor always substitutes the
///     property's value with the redacted sentinel, regardless of whether its name matches the built-in
///     sensitive-name patterns, even when <see cref="AuditLogAttribute" /> is also applied to the same property;
///     and for role-gated API response filtering (see <c>DKNet.EfCore.Extensions.Serialization.SensitiveDataJsonExtensions</c>),
///     the property is withheld from a serialized response unless the caller is authenticated and holds one of
///     the named <see cref="Roles" />.
/// </summary>
/// <param name="roles">
///     The roles permitted to receive this property in a role-aware serialized response. Never
///     <see langword="null" />; an empty list means any authenticated caller is permitted. Has no effect on
///     audit-log redaction, which is unconditional.
/// </param>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class SensitiveDataAttribute(params string[] roles) : Attribute
{
    /// <summary>
    ///     Declares the property sensitive to any authenticated caller — names no roles. Kept as an
    ///     explicit constructor, rather than relying solely on a zero-argument call into the primary
    ///     constructor's <c>params</c> array, so <c>[SensitiveData]</c> still compiles to a true
    ///     parameterless <c>.ctor()</c> in the assembly's metadata. That preserves binary compatibility
    ///     for a consumer assembly already compiled against the marker-only shape that shipped before
    ///     <see cref="Roles" /> was added (round-1 review finding).
    /// </summary>
    public SensitiveDataAttribute() : this([])
    {
    }

    /// <summary>
    ///     The roles permitted to receive this property in a role-aware serialized response. Never
    ///     <see langword="null" />; empty means any authenticated caller is permitted. An explicit
    ///     <see langword="null" /> array argument (e.g. <c>[SensitiveData(null)]</c>) collapses to empty
    ///     rather than propagating the null.
    /// </summary>
    public IReadOnlyList<string> Roles { get; } = roles ?? [];
}
