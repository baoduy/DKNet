using DKNet.EfCore.Extensions.Snapshots;

namespace EfCore.HookTests.Data;

/// <summary>
///     Hook that depends on a scoped marker service. Records which instance and value it observed so
///     tests can assert hooks resolve scoped dependencies from the DbContext's own scope.
/// </summary>
public sealed class ScopedStateHook(TestScopedMarker marker) : HookAsync
{
    #region Properties

    /// <summary>
    ///     The marker instance this hook was given, available directly for reference assertions.
    /// </summary>
    public TestScopedMarker Marker { get; } = marker;

    /// <summary>
    ///     The marker instance observed during the last <see cref="BeforeSaveAsync" /> invocation.
    /// </summary>
    public static TestScopedMarker? ObservedMarker { get; private set; }

    /// <summary>
    ///     The per-request value observed during the last <see cref="BeforeSaveAsync" /> invocation.
    /// </summary>
    public static string? ObservedValue { get; private set; }

    #endregion

    #region Methods

    public override Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        ObservedMarker = Marker;
        ObservedValue = Marker.RequestValue;
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Clears the observed state captured by previous invocations.
    /// </summary>
    public static void Reset()
    {
        ObservedMarker = null;
        ObservedValue = null;
    }

    #endregion
}