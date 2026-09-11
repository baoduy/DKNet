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
    ///     The roles permitted to receive this property in a role-aware serialized response. Never
    ///     <see langword="null" />; empty means any authenticated caller is permitted.
    /// </summary>
    public IReadOnlyList<string> Roles { get; } = roles;
}
