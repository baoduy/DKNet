// Test-local double proving DRK-1569 / LOG-002's name-based match (R1): a type named
// "EncryptedAttribute" declared in a namespace unrelated to DKNet.EfCore.Encryption.Attributes must still
// redact, since SensitiveDataPatterns.IsEncrypted matches by attribute type name, never by type identity.

namespace EfCore.AuditLogs.Tests.ForeignAttributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class EncryptedAttribute : Attribute;
