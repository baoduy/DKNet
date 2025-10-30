namespace DKNet.EfCore.Abstractions.Attributes;

/// <summary>
///     Include the Entities to the Audit Log.
///     When this attribute is applied to an entity class, changes made to instances of that class
///     will be recorded in the audit logs, provided that the entity implements the necessary
///     auditing interfaces (e.g., IAuditedProperties). This attribute is useful for explicitly marking entities
///     that should be tracked for auditing purposes.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AuditLogAttribute : Attribute;