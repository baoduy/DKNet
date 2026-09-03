using System.Collections.Concurrent;
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.AuditLogs.Internals;
using DKNet.Fw.Extensions;
using DKNet.Fw.Extensions.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DKNet.EfCore.AuditLogs;

internal static class AuditLogExtensions
{
    #region Fields

    // Per-entity-type audit metadata, resolved once via reflection and reused for every save.
    // None of the underlying attributes can change at runtime, so caching by CLR type is safe.
    private static readonly ConcurrentDictionary<Type, EntityAuditPlan> _plans = new();

    #endregion

    #region Methods

    public static AuditLogEntry? BuildAuditLog(
        this EntityEntry entry,
        EntityState originalState,
        AuditLogBehaviour behaviour,
        AuditPropertyPolicy propertyPolicy)
    {
        // If behaviour is OnlyAttributedAuditedEntities, skip entities not marked with AuditLogAttribute
        if (entry.Entity is not IAuditedProperties audited)
        {
            return null;
        }

        var entityType = entry.Entity.GetType();
        var plan = _plans.GetOrAdd(entityType, static (t, e) => BuildPlan(t, e), entry);

        // if explicitly ignored with IgnoreAuditLogAttribute, skip
        if (plan.Ignore)
        {
            return null;
        }

        // if OnlyAttributedAuditedEntities, skip if not marked with AuditLogAttribute
        if (behaviour == AuditLogBehaviour.OnlyAttributedAuditedEntities && !plan.HasAuditLogAttribute)
        {
            return null;
        }

        //Collect the change fields.
        var changes = new List<AuditFieldChange>();

        if (originalState != EntityState.Added)
        {
            // For OnlyAttributedProperties, drive the loop from the precomputed attributed-property
            // list instead of scanning every property on the entity.
            var propertiesToScan = propertyPolicy == AuditPropertyPolicy.OnlyAttributedProperties
                ? plan.AttributedPropertyNames.Select(entry.Property)
                : entry.Properties;

            foreach (var prop in propertiesToScan)
            {
                var propPlan = plan.Properties[prop.Metadata.Name];

                // NEW: skip property-level IgnoreAuditLogAttribute
                if (propPlan.Ignore)
                {
                    continue;
                }

                var isSensitive = propPlan.Sensitive;

                var name = prop.Metadata.Name;
                var oldVal = prop.OriginalValue;
                var newVal = prop.CurrentValue;

                if (originalState == EntityState.Deleted)
                {
                    changes.Add(
                        new AuditFieldChange
                        {
                            FieldName = name,
                            OldValue = isSensitive ? SensitiveDataPatterns.RedactedValue : oldVal,
                            NewValue = null
                        });
                    continue;
                }

                if (prop.IsModified || !Equals(oldVal, newVal))
                {
                    changes.Add(
                        new AuditFieldChange
                        {
                            FieldName = name,
                            OldValue = isSensitive ? SensitiveDataPatterns.RedactedValue : oldVal,
                            NewValue = isSensitive ? SensitiveDataPatterns.RedactedValue : newVal
                        });
                }
            }
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

    /// <summary>
    ///     Resolves the attribute-driven audit metadata for an entity type once, by inspecting the given
    ///     <paramref name="entry" />; the result is cached in <see cref="_plans" /> and reused for every
    ///     subsequent save of that type.
    /// </summary>
    private static EntityAuditPlan BuildPlan(Type entityType, EntityEntry entry)
    {
        var ignore = entityType.HasAttribute<IgnoreAuditLogAttribute>();
        var hasAuditLogAttribute = entityType.HasAttribute<AuditLogAttribute>();

        var properties = new Dictionary<string, PropertyAuditPlan>();
        var attributedNames = new List<string>();

        foreach (var prop in entry.Properties)
        {
            var clrProp = prop.Metadata.PropertyInfo;
            var propIgnore = clrProp.HasAttribute<IgnoreAuditLogAttribute>();
            var attributed = clrProp.HasAttribute<AuditLogAttribute>();
            var declaredSensitive = clrProp.HasAttribute<SensitiveDataAttribute>();
            var sensitive = declaredSensitive || (!attributed && SensitiveDataPatterns.IsSensitive(clrProp));

            properties[prop.Metadata.Name] = new PropertyAuditPlan(propIgnore, sensitive);

            if (attributed && !propIgnore)
            {
                attributedNames.Add(prop.Metadata.Name);
            }
        }

        return new EntityAuditPlan(ignore, hasAuditLogAttribute, properties, attributedNames);
    }

    #endregion

    #region Nested Types

    private sealed record PropertyAuditPlan(bool Ignore, bool Sensitive);

    private sealed record EntityAuditPlan(
        bool Ignore,
        bool HasAuditLogAttribute,
        IReadOnlyDictionary<string, PropertyAuditPlan> Properties,
        IReadOnlyList<string> AttributedPropertyNames);

    #endregion
}