using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DKNet.EfCore.AuditLogs;

internal static class AuditLogExtensions
{
    public static AuditLogEntry? BuildAuditLog(this EntityEntry entry, EntityState originalState)
    {
        if (entry.Entity is not IAuditedProperties audited) return null;
        var entityType = entry.Entity.GetType();
        if (Attribute.IsDefined(entityType, typeof(IgnoreAuditLogAttribute)))
            return null;

        var changes = new List<AuditFieldChange>();
        foreach (var prop in entry.Properties)
        {
            // Skip navigation and concurrency tokens if desired (extend later)
            var name = prop.Metadata.Name;
            var oldVal = prop.OriginalValue;
            var newVal = prop.CurrentValue;

            if (originalState == EntityState.Deleted)
            {
                changes.Add(new AuditFieldChange
                {
                    FieldName = name,
                    OldValue = oldVal,
                    NewValue = null
                });
                continue;
            }

            if (prop.IsModified || !Equals(oldVal, newVal))
                changes.Add(new AuditFieldChange
                {
                    FieldName = name,
                    OldValue = oldVal,
                    NewValue = newVal
                });
        }

        var action = originalState switch
        {
            EntityState.Deleted => AuditLogAction.Deleted,
            EntityState.Added => AuditLogAction.Created,
            _ => AuditLogAction.Updated
        };

        return new AuditLogEntry
        {
            Keys = entry.GetEntityKeyValues(),
            CreatedBy = audited.CreatedBy,
            CreatedOn = audited.CreatedOn,
            UpdatedBy = audited.UpdatedBy,
            UpdatedOn = audited.UpdatedOn,
            EntityName = entry.Entity.GetType().Name,
            Action = action,
            Changes = changes
        };
    }
}