namespace DKNet.EfCore.Abstractions.Attributes;

/// <summary>
///     Include the Entities or properties to the Audit Log.
///     When this attribute is applied to an entity class, changes made to instances of that class
///     will be recorded in the audit logs, provided that the entity implements the necessary
///     auditing interfaces (e.g., IAuditedProperties). This attribute is useful for explicitly marking entities
///     that should be tracked for auditing purposes.
/// </summary>
/// <remarks>
///     When applied to a property instead, it has a second, independent meaning: under the default
///     property policy (<c>AuditPropertyPolicy.RedactSensitive</c>) it forces plaintext capture of that
///     property even if its name matches the sensitive-data pattern list; under the strict
///     <c>AuditPropertyPolicy.OnlyAttributedProperties</c> policy it marks the property as allow-listed
///     for capture at all.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, Inherited = false)]
public sealed class AuditLogAttribute : Attribute;