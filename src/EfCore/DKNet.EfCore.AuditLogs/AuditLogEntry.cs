using DKNet.EfCore.Abstractions.Entities;

namespace DKNet.EfCore.AuditLogs;

public sealed record AuditFieldChange
{
    #region Properties

    public required string FieldName { get; init; }
    public object? NewValue { get; init; }
    public object? OldValue { get; init; }

    #endregion
}

public enum AuditLogAction
{
    Created,
    Updated,
    Deleted
}

public sealed record AuditLogEntry : IAuditedProperties
{
    #region Properties

    public required AuditLogAction Action { get; init; }
    public required IReadOnlyList<AuditFieldChange> Changes { get; init; }
    public required string CreatedBy { get; init; }
    public required DateTimeOffset CreatedOn { get; init; }
    public required string EntityName { get; init; }
    public required IDictionary<string, object?> Keys { get; init; }
    public string? UpdatedBy { get; init; }
    public DateTimeOffset? UpdatedOn { get; init; }

    #endregion
}