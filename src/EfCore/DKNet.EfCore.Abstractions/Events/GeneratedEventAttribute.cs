namespace DKNet.EfCore.Abstractions.Events;

/// <summary>
///     Assembly-level registration linking an entity type to a source-generator-emitted domain event
///     record for one or more lifecycle operations.
/// </summary>
/// <remarks>
///     Emitted by <c>DKNet.EfCore.DtoGenerator</c>'s event generator for every <c>[GenerateEvent]</c>
///     declaration found on an entity. <c>DKNet.EfCore.Events</c> reads these assembly attributes at
///     runtime (see <c>DeclaredEventRegistry</c>) to raise the declared event automatically after a
///     successful save. This attribute is not intended to be applied by hand.
/// </remarks>
/// <param name="entityType">The entity type the declaration was made on.</param>
/// <param name="eventType">The generated event record type to raise.</param>
/// <param name="operations">The lifecycle operation(s) that raise <paramref name="eventType" />.</param>
/// <param name="properties">
///     For <see cref="EventOperations.Updated" />, the narrowing property list: the event raises only when
///     at least one of these properties changed. Empty means any change qualifies.
/// </param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class GeneratedEventAttribute(
    Type entityType,
    Type eventType,
    EventOperations operations,
    params string[] properties) : Attribute
{
    /// <summary>
    ///     Gets the entity type the declaration was made on.
    /// </summary>
    public Type EntityType { get; } = entityType;

    /// <summary>
    ///     Gets the generated event record type to raise.
    /// </summary>
    public Type EventType { get; } = eventType;

    /// <summary>
    ///     Gets the lifecycle operation(s) that raise <see cref="EventType" />.
    /// </summary>
    public EventOperations Operations { get; } = operations;

    /// <summary>
    ///     Gets the <see cref="EventOperations.Updated" /> narrowing property list. Empty means any
    ///     property change qualifies.
    /// </summary>
    public IReadOnlyList<string> Properties { get; } = properties;
}
