// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DKNet.EfCore.Abstractions.Attributes;

/// <summary>
///     The IgnoreAuditLog attribute can be applied to entity classes to exclude them from audit logging.
///     When this attribute is present on an entity class, any changes made to instances of that class
///     will not be recorded in the audit logs. This is useful for entities that do not require auditing,
///     such as those used for temporary data or logging purposes.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, Inherited = false)]
public sealed class IgnoreAuditLogAttribute : Attribute;