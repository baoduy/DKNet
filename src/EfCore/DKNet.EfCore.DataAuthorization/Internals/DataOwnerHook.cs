using System.Reflection;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Extensions.Snapshots;
using DKNet.EfCore.Hooks;
using DKNet.Fw.Extensions;
using DKNet.Fw.Extensions.Reflection;
using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.DataAuthorization.Internals;

/// <summary>
///     Implements a hook that manages data ownership assignments before saving entities.
/// </summary>
/// <remarks>
///     This hook is responsible for:
///     - Automatically setting ownership information on newly created entities
///     - Automatically stamping the modifier and modification time on modified entities
///     - Ensuring proper data authorization context is maintained
///     - Managing entity ownership during the save process
/// </remarks>
/// <remarks>
///     Initializes a new instance of the <see cref="DataOwnerHook" /> class.
/// </remarks>
/// <param name="dataOwnerProvider">The provider that supplies ownership information.</param>
internal sealed class DataOwnerHook(IDataOwnerProvider dataOwnerProvider) : IBeforeSaveHookAsync
{
    #region Methods

    /// <summary>
    ///     Executes before saving changes to ensure proper ownership assignment.
    /// </summary>
    /// <param name="context">The snapshot context containing entity changes.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        UpdatingOwner(context);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Updates the ownership information for newly added entities, guards existing ownership on modified
    ///     entities against silent reassignment, and stamps the modifier and modification time on modified
    ///     entities.
    /// </summary>
    /// <param name="context">The snapshot context containing entity changes.</param>
    private void UpdatingOwner(SnapshotContext context)
    {
        var autoDetectChangesEnabled = context.DbContext.ChangeTracker.AutoDetectChangesEnabled;
        context.DbContext.ChangeTracker.AutoDetectChangesEnabled = true;

        try
        {
            var ownerKey = dataOwnerProvider.GetOwnershipKey();
            var accessibleKeys = dataOwnerProvider.GetAccessibleKeys();

            foreach (var entry in context.Entities)
                switch (entry.OriginalState)
                {
                    case EntityState.Added when !string.IsNullOrEmpty(ownerKey):
                        StampAddedEntity(entry.Entity, ownerKey);
                        break;

                    case EntityState.Modified:
                        GuardOwnedByReassignment(entry, accessibleKeys);
                        if (!string.IsNullOrEmpty(ownerKey)) StampModifiedEntity(entry, ownerKey);
                        break;
                }
        }
        finally
        {
            context.DbContext.ChangeTracker.AutoDetectChangesEnabled = autoDetectChangesEnabled;
        }
    }

    /// <summary>
    ///     Stamps audit and ownership properties on a newly added entity.
    /// </summary>
    /// <param name="entity">The newly added entity.</param>
    /// <param name="ownerKey">The ownership key of the current context (guaranteed non-empty).</param>
    private static void StampAddedEntity(object entity, string ownerKey)
    {
        if (entity is IAuditedProperties au && string.IsNullOrEmpty(au.CreatedBy))
        {
            SetOwnedProperty(au, nameof(au.CreatedBy), ownerKey);
            SetOwnedProperty(au, nameof(au.CreatedOn), DateTimeOffset.UtcNow);
        }

        if (entity is IOwnedBy own && string.IsNullOrEmpty(own.OwnedBy))
            SetOwnedProperty(own, nameof(IOwnedBy.OwnedBy), ownerKey);
    }

    /// <summary>
    ///     Stamps modification audit properties on a modified entity with the current context's ownership key,
    ///     unless an explicit modifier was already supplied for this change set.
    /// </summary>
    /// <param name="entry">The snapshot entry for the modified entity.</param>
    /// <param name="ownerKey">The ownership key of the current context (guaranteed non-empty).</param>
    /// <remarks>
    ///     An explicit modifier is detected by comparing <see cref="IAuditedProperties.UpdatedBy" />'s current
    ///     value against its <c>OriginalValue</c> for this change set: a difference means a domain method already
    ///     set it, so both <see cref="IAuditedProperties.UpdatedBy" /> and <see cref="IAuditedProperties.UpdatedOn" />
    ///     are left untouched. Otherwise the value is overwritten, even when a prior save already recorded a
    ///     modifier, so the latest save always wins.
    /// </remarks>
    private static void StampModifiedEntity(SnapshotEntityEntry entry, string ownerKey)
    {
        if (entry.Entity is not IAuditedProperties au) return;
        if (entry.Entry.Metadata.FindProperty(nameof(IAuditedProperties.UpdatedBy)) is null) return;

        var originalUpdatedBy = entry.Entry.Property(nameof(IAuditedProperties.UpdatedBy)).OriginalValue as string;
        if (!string.Equals(originalUpdatedBy, au.UpdatedBy, StringComparison.Ordinal)) return;

        SetOwnedProperty(au, nameof(IAuditedProperties.UpdatedBy), ownerKey);
        SetOwnedProperty(au, nameof(IAuditedProperties.UpdatedOn), DateTimeOffset.UtcNow);
    }

    /// <summary>
    ///     Sets a property's value on <paramref name="entity" />, resolving the writable accessor by walking up
    ///     the type hierarchy (see <see cref="FindWritableProperty" />) instead of the single-type lookup that
    ///     <see cref="PropertyExtensions.GetProperty{T}" /> performs.
    /// </summary>
    /// <param name="entity">The object to set the property on.</param>
    /// <param name="propertyName">The name of the property to set.</param>
    /// <param name="value">The value to set.</param>
    /// <exception cref="ArgumentException">
    ///     No writable property named <paramref name="propertyName" /> exists anywhere in <paramref name="entity" />'s
    ///     type hierarchy.
    /// </exception>
    private static void SetOwnedProperty(object entity, string propertyName, object value)
    {
        var property = FindWritableProperty(entity.GetType(), propertyName) ??
                       throw new ArgumentException(
                           $"Property '{propertyName}' not found on type '{entity.GetType().FullName}'.",
                           nameof(propertyName));

        entity.SetPropertyValue(property, value);
    }

    /// <summary>
    ///     Finds a property by name, walking up from <paramref name="type" /> through its base types.
    /// </summary>
    /// <remarks>
    ///     <see cref="Type.GetProperty(string, BindingFlags)" /> on a derived type only resolves non-public
    ///     accessors declared directly on that type — it does not see a non-public setter declared on a base
    ///     class, exactly the "private setter + intention-revealing method" pattern this codebase favors
    ///     (e.g. <c>AuditedEntity&lt;TKey&gt;</c>). Searching each type in the hierarchy with
    ///     <see cref="BindingFlags.DeclaredOnly" /> finds it.
    /// </remarks>
    /// <param name="type">The runtime type to start searching from.</param>
    /// <param name="propertyName">The name of the property to find.</param>
    /// <returns>The writable <see cref="PropertyInfo" />, or <c>null</c> if none exists in the hierarchy.</returns>
    private static PropertyInfo? FindWritableProperty(Type type, string propertyName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var property = current.GetProperty(propertyName,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.DeclaredOnly);

            if (property?.GetSetMethod(true) is not null) return property;
        }

        return null;
    }

    /// <summary>
    ///     Reverts a modified entity's <see cref="IOwnedBy.OwnedBy" /> to its original value unless the new value
    ///     is one of the current context's accessible keys, preventing cross-tenant transfer and orphaning.
    /// </summary>
    /// <param name="entry">The snapshot entry for the modified entity.</param>
    /// <param name="accessibleKeys">The data keys the current context may reassign ownership to.</param>
    private static void GuardOwnedByReassignment(SnapshotEntityEntry entry, ICollection<string> accessibleKeys)
    {
        if (entry.Entity is not IOwnedBy own) return;
        if (entry.Entry.Metadata.FindProperty(nameof(IOwnedBy.OwnedBy)) is null) return;

        var original = entry.Entry.Property(nameof(IOwnedBy.OwnedBy)).OriginalValue as string;
        var current = own.OwnedBy;

        if (string.Equals(current, original, StringComparison.Ordinal)) return;
        if (!string.IsNullOrEmpty(current) && accessibleKeys.Contains(current)) return;

        // Not accessible (or blank) — revert to the original owner so the row never moves to another
        // tenant and never becomes orphaned.
        var property = FindWritableProperty(own.GetType(), nameof(IOwnedBy.OwnedBy));
        if (property is not null) own.TrySetPropertyValue(property, original ?? string.Empty);
    }

    #endregion
}