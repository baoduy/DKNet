using DKNet.EfCore.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DKNet.EfCore.AuditLogs;

internal static class Extensions
{
    public static EfCoreAuditLog? BuildAuditLog(this EntityEntry entry, EntityState originalState)
    {
        if (entry.Entity is not IAuditedProperties audited) return null;

        var changes = new List<EfCoreAuditFieldChange>();
        foreach (var prop in entry.Properties)
        {
            // Skip navigation and concurrency tokens if desired (extend later)
            var name = prop.Metadata.Name;
            var oldVal = prop.OriginalValue;
            var newVal = prop.CurrentValue;

            if (originalState == EntityState.Deleted)
            {
                // Treat deletion as old value -> null
                changes.Add(new EfCoreAuditFieldChange
                {
                    FieldName = name,
                    OldValue = oldVal,
                    NewValue = null
                });
                continue;
            }

            // Modified: only include if value actually changed or EF marked it modified
            if (prop.IsModified || !Equals(oldVal, newVal))
                changes.Add(new EfCoreAuditFieldChange
                {
                    FieldName = name,
                    OldValue = oldVal,
                    NewValue = newVal
                });
        }

        return new EfCoreAuditLog
        {
            CreatedBy = audited.CreatedBy,
            CreatedOn = audited.CreatedOn,
            UpdatedBy = audited.UpdatedBy,
            UpdatedOn = audited.UpdatedOn,
            EntityName = entry.Entity.GetType().Name,
            Changes = changes
        };
    }
}