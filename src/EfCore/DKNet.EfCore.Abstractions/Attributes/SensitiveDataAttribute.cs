// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DKNet.EfCore.Abstractions.Attributes;

/// <summary>
///     The SensitiveData attribute can be applied to an entity property to declare it sensitive for audit-log
///     redaction purposes, regardless of whether its name matches the built-in sensitive-name patterns. When
///     present, the audit-log redactor always substitutes the property's value with the redacted sentinel,
///     even when <see cref="AuditLogAttribute" /> is also applied to the same property.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class SensitiveDataAttribute : Attribute;
