using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.Fw.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DKNet.EfCore.AuditLogs;

internal static class AuditLogExtensions
{
    #region Methods

    public static AuditLogEntry? BuildAuditLog(this EntityEntry entry, EntityState originalState,
        AuditLogBehaviour behaviour)
    {
        // If behaviour is OnlyAttributedAuditedEntities, skip entities not marked with AuditLogAttribute
        if (entry.Entity is not IAuditedProperties audited) return null;
        var entityType = entry.Entity.GetType();
        // if explicitly ignored with IgnoreAuditLogAttribute, skip
        if (entityType.HasAttribute<IgnoreAuditLogAttribute>())
            return null;
        // if OnlyAttributedAuditedEntities, skip if not marked with AuditLogAttribute
        if (behaviour == AuditLogBehaviour.OnlyAttributedAuditedEntities &&
            !entityType.HasAttribute<AuditLogAttribute>())
            return null;

        //Collect the change fields.
        var changes = new List<AuditFieldChange>();

        if (originalState != EntityState.Added)
            foreach (var prop in entry.Properties)
            {
                // NEW: skip property-level IgnoreAuditLogAttribute
                var clrProp = prop.Metadata.PropertyInfo;
                if (clrProp.HasAttribute<IgnoreAuditLogAttribute>())
                    continue;

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

        // Determine action
        var action = originalState switch
        {
            EntityState.Deleted => AuditLogAction.Deleted,
            EntityState.Added => AuditLogAction.Created,
            _ => AuditLogAction.Updated
        };

        // create log entry
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

    #endregion
}