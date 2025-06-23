using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Extensions.Snapshots;

namespace DKNet.EfCore.DataAuthorization.Internals;

/// <summary>
/// Implements a hook that manages data ownership assignments before saving entities.
/// </summary>
/// <remarks>
/// This hook is responsible for:
/// - Automatically setting ownership information on newly created entities
/// - Ensuring proper data authorization context is maintained
/// - Managing entity ownership during the save process
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="DataOwnerHook"/> class.
/// </remarks>
/// <param name="dataOwnerProvider">The provider that supplies ownership information.</param>
internal sealed class DataOwnerHook(IDataOwnerProvider dataOwnerProvider) : IBeforeSaveHookAsync
{
    /// <summary>
    /// Executes before saving changes to ensure proper ownership assignment.
    /// </summary>
    /// <param name="context">The snapshot context containing entity changes.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task RunBeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        UpdatingOwner(context);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Updates the ownership information for newly added entities.
    /// </summary>
    /// <param name="context">The snapshot context containing entity changes.</param>
    private void UpdatingOwner(SnapshotContext context)
    {
        context.DbContext.ChangeTracker.AutoDetectChangesEnabled = true;

        var dataKeyEntities = context.SnapshotEntities
            .Where(e => e.OriginalState == EntityState.Added)
            .Select(e => e.Entity)
            .OfType<IOwnedBy>();

        foreach (var entity in dataKeyEntities)
            entity.SetOwnedBy(dataOwnerProvider.GetOwnershipKey());
    }
}