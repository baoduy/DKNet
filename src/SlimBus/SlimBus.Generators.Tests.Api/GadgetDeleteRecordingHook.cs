using System.Collections.Concurrent;
using DKNet.EfCore.Extensions.Snapshots;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using SlimBus.Generators.Tests.Domain.Catalog;

namespace SlimBus.Generators.Tests.Api;

/// <summary>
///     DRK-1326 §5 scenario 7 observation seam ("no audit entry and no domain event are raised"): a
///     singleton recorder of every <see cref="Gadget" /> id that reached <c>SaveChangesAsync</c> marked for
///     deletion. A refused delete never calls <c>SaveChangesAsync</c> at all, so its id never appears here —
///     this is the chosen seam over a recording <c>Fluents.EventsConsumers.IHandler{T}</c> because Gadget's
///     delete path raises no domain event today (only its constructor does), so an event consumer would have
///     nothing to observe either way.
/// </summary>
public sealed class GadgetSaveAttemptRecorder
{
    public ConcurrentBag<Guid> DeletedGadgetIds { get; } = [];
}

/// <summary>Records every Gadget id marked <see cref="EntityState.Deleted" /> that reaches <c>BeforeSaveAsync</c>.</summary>
public sealed class GadgetDeleteRecordingHook(GadgetSaveAttemptRecorder recorder) : HookAsync
{
    /// <inheritdoc />
    public override Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entities)
            if (entry.OriginalState == EntityState.Deleted && entry.Entity is Gadget gadget)
                recorder.DeletedGadgetIds.Add(gadget.Id);

        return Task.CompletedTask;
    }
}
