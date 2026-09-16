using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.AuditLogs.Internals;
using DKNet.EfCore.Extensions.Snapshots;
using DKNet.EfCore.Hooks;
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
/// <param name="currentUserProvider">
///     The optional signed-in-user provider. When registered and returning a non-empty value, it takes
///     precedence over <paramref name="dataOwnerProvider" /> for <c>CreatedBy</c>/<c>UpdatedBy</c> — this
///     hook then stamps <c>OwnedBy</c> only, leaving <c>CreatedBy</c>/<c>CreatedOn</c>/<c>UpdatedBy</c>/
///     <c>UpdatedOn</c> for <c>EfCoreAuditHook</c> to stamp instead. The decision is made independently by
///     each hook from <see cref="ICurrentUserProvider" />'s own value, never from hook run order.
/// </param>
internal sealed class DataOwnerHook(IDataOwnerProvider dataOwnerProvider, ICurrentUserProvider? currentUserProvider = null)
    : IBeforeSaveHookAsync
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
            var stampAuditFromOwner = string.IsNullOrEmpty(currentUserProvider?.GetCurrentUser());

            foreach (var entry in context.Entities)
                switch (entry.OriginalState)
                {
                    case EntityState.Added when !string.IsNullOrEmpty(ownerKey):
                        StampAddedEntity(entry, ownerKey, stampAuditFromOwner);
                        break;

                    case EntityState.Modified:
                        GuardOwnedByReassignment(entry, accessibleKeys);
                        if (!string.IsNullOrEmpty(ownerKey) && stampAuditFromOwner)
                            AuditPropertyStamper.StampUpdatedBy(entry, ownerKey);
                        break;
                }
        }
        finally
        {
            context.DbContext.ChangeTracker.AutoDetectChangesEnabled = autoDetectChangesEnabled;
        }
    }

    /// <summary>
    ///     Stamps <see cref="IOwnedBy.OwnedBy" /> on a newly added entity, and — only when no
    ///     <see cref="ICurrentUserProvider" /> value takes precedence — <c>CreatedBy</c>/<c>CreatedOn</c> too.
    /// </summary>
    /// <param name="entry">The snapshot entry for the newly added entity.</param>
    /// <param name="ownerKey">The ownership key of the current context (guaranteed non-empty).</param>
    /// <param name="stampAuditFromOwner">
    ///     Whether <c>CreatedBy</c>/<c>CreatedOn</c> should be stamped from <paramref name="ownerKey" /> —
    ///     <see langword="false" /> when a registered <see cref="ICurrentUserProvider" /> is supplying them
    ///     instead.
    /// </param>
    private static void StampAddedEntity(SnapshotEntityEntry entry, string ownerKey, bool stampAuditFromOwner)
    {
        var entity = entry.Entity;

        if (stampAuditFromOwner) AuditPropertyStamper.StampCreatedBy(entry, ownerKey);

        if (entity is IOwnedBy own && string.IsNullOrEmpty(own.OwnedBy))
            AuditPropertyStamper.SetOwnedProperty(entry.Entry, own, nameof(IOwnedBy.OwnedBy), ownerKey);
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
        // tenant and never becomes orphaned. The FindProperty guard above already confirmed OwnedBy is
        // part of the EF model, so the compiled accessor always applies here.
        entry.Entry.Property(nameof(IOwnedBy.OwnedBy)).CurrentValue = original ?? string.Empty;
    }

    #endregion
}