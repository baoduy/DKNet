namespace DKNet.EfCore.Abstractions.Attributes;

/// <summary>
///     Specifies that an Entity class should be ignored by the automatic entity mapper.
///     This attribute is primarily intended for use with delivered types where automatic mapping is not desired.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IgnoreEntityAttribute : Attribute;

/// <summary>
/// The IgnoreAuditLog attribute can be applied to entity classes to exclude them from audit logging.
/// When this attribute is present on an entity class, any changes made to instances of that class
/// will not be recorded in the audit logs. This is useful for entities that do not require auditing,
/// such as those used for temporary data or logging purposes.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IgnoreAuditLogAttribute : Attribute;