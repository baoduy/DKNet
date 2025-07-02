// ReSharper disable CheckNamespace

using DKNet.EfCore.Extensions.Snapshots;

namespace Microsoft.EntityFrameworkCore;

public static class SnapshotExtensions
{
    public static SnapshotContext Snapshot(this DbContext context) => new(context);
}