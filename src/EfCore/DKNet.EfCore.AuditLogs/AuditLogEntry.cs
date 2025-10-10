using DKNet.EfCore.Abstractions.Entities;

namespace DKNet.EfCore.AuditLogs;

public sealed record AuditFieldChange
{
    public required string FieldName { get; init; }
    public object? OldValue { get; init; }
    public object? NewValue { get; init; }
}

public enum AuditLogAction
{
    Created,
    Updated,
    Deleted
}

public sealed record AuditLogEntry : IAuditedProperties
{
    public required IDictionary<string, object?> Keys { get; init; }
    public required string CreatedBy { get; init; }
    public required DateTimeOffset CreatedOn { get; init; }
    public string? UpdatedBy { get; init; }
    public DateTimeOffset? UpdatedOn { get; init; }
    public required string EntityName { get; init; }

    public required AuditLogAction Action { get; init; }
    public required IReadOnlyList<AuditFieldChange> Changes { get; init; }
}