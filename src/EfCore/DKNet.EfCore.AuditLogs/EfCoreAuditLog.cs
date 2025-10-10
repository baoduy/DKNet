using DKNet.EfCore.Abstractions.Entities;

namespace DKNet.EfCore.AuditLogs;

public sealed record EfCoreAuditFieldChange
{
    public required string FieldName { get; init; }
    public object? OldValue { get; init; }
    public object? NewValue { get; init; }
}

public sealed record EfCoreAuditLog : IAuditedProperties
{
    public required string CreatedBy { get; init; }
    public required DateTimeOffset CreatedOn { get; init; }
    public string? UpdatedBy { get; init; }
    public DateTimeOffset? UpdatedOn { get; init; }
    public required string EntityName { get; init; }
    public required IReadOnlyList<EfCoreAuditFieldChange> Changes { get; init; }
}