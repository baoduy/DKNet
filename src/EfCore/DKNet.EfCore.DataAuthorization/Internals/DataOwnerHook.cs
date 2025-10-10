using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Extensions.Snapshots;
using DKNet.EfCore.Hooks;
using DKNet.Fw.Extensions;
using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.DataAuthorization.Internals;

/// <summary>
///     Implements a hook that manages data ownership assignments before saving entities.
/// </summary>
/// <remarks>
///     This hook is responsible for:
///     - Automatically setting ownership information on newly created entities
///     - Ensuring proper data authorization context is maintained
///     - Managing entity ownership during the save process
/// </remarks>
/// <remarks>
///     Initializes a new instance of the <see cref="DataOwnerHook" /> class.
/// </remarks>
/// <param name="dataOwnerProvider">The provider that supplies ownership information.</param>
internal sealed class DataOwnerHook(IDataOwnerProvider dataOwnerProvider) : IBeforeSaveHookAsync
{
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
    ///     Updates the ownership information for newly added entities.
    /// </summary>
    /// <param name="context">The snapshot context containing entity changes.</param>
    private void UpdatingOwner(SnapshotContext context)
    {
        context.DbContext.ChangeTracker.AutoDetectChangesEnabled = true;

        var dataKeyEntities = context.Entities
            .Where(e => e.OriginalState == EntityState.Added)
            .Select(e => e.Entity);

        var ownerKey = dataOwnerProvider.GetOwnershipKey();
        if (string.IsNullOrEmpty(ownerKey)) return;

        foreach (var entity in dataKeyEntities)
        {
            if (entity is IAuditedProperties au && string.IsNullOrEmpty(au.CreatedBy))
            {
                au.SetPropertyValue(nameof(au.CreatedBy), ownerKey);
                au.SetPropertyValue(nameof(au.CreatedOn), DateTimeOffset.Now);
            }

            if (entity is IOwnedBy own && string.IsNullOrEmpty(own.OwnedBy))
                own.TrySetPropertyValue(nameof(IOwnedBy.OwnedBy), ownerKey);
        }
    }
}