namespace EfCore.HookTests.Data;

/// <summary>
///     Scoped marker service modelling a per-request state carrier (like an IDataOwnerProvider that a
///     middleware initialises once per request). Hook resolution must observe the same scoped instance
///     as the DbContext being saved.
/// </summary>
public sealed class TestScopedMarker
{
    /// <summary>
    ///     Value set once per request on the scoped instance.
    /// </summary>
    public string RequestValue { get; set; } = string.Empty;
}