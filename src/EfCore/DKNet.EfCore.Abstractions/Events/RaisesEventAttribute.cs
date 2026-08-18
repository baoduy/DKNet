namespace DKNet.EfCore.Abstractions.Events;

/// <summary>
///     Declares that the carrying entity raises a generated event payload record for one or more
///     persistence operations after a successful save. Repeatable — apply once per event the entity raises.
/// </summary>
/// <remarks>
///     <para>
///     Usage:
///     <code>
///     [GenerateDto(typeof(Order), Exclude = new[] { "InternalNote" })]
///     public partial record OrderPlacedEvent;
///
///     [RaisesEvent(typeof(OrderPlacedEvent), EventOperations.Created)]
///     [RaisesEvent(typeof(OrderStatusChangedEvent), EventOperations.Updated, nameof(Order.Status))]
///     public class Order { public string Status { get; set; } = string.Empty; }
///     </code>
///     </para>
///     <para>
///     <paramref name="eventType"/> must be a payload record generated via <c>[GenerateDto]</c> from the SAME
///     entity type this attribute is applied to; a build error is raised otherwise.
///     </para>
///     <para>
///     <paramref name="properties"/> narrows an <see cref="EventOperations.Updated"/> rule: non-empty raises
///     only when at least one listed property changed (use <c>nameof(Entity.Property)</c> for compiler-checked
///     names). Empty raises on any change. Entries must be a direct property of the entity — nested paths are
///     not supported and fail the build. Narrowing on a rule whose <paramref name="operations"/> has no
///     <see cref="EventOperations.Updated"/> flag is ignored at runtime and reported as a build warning.
///     </para>
///     <para>
///     This attribute alone does not raise anything: the application must register <c>DKNet.EfCore.Events</c>'
///     save hook (exactly as for hand-raised events) before declared events publish. A domain project that
///     only references <c>DKNet.EfCore.Abstractions</c> and <c>DKNet.EfCore.DtoGenerator</c> builds cleanly
///     with rules declared and simply never raises them until the application wires up the runtime.
///     </para>
/// </remarks>
/// <param name="eventType">The <c>[GenerateDto]</c>-generated payload record type to raise.</param>
/// <param name="operations">The persistence operation(s) that raise <paramref name="eventType"/>.</param>
/// <param name="properties">
///     For <see cref="EventOperations.Updated"/>, the narrowing property list. Empty means any change qualifies.
/// </param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class RaisesEventAttribute(Type eventType, EventOperations operations, params string[] properties)
    : Attribute
{
    /// <summary>
    ///     Gets the <c>[GenerateDto]</c>-generated payload record type to raise.
    /// </summary>
    public Type EventType { get; } = eventType;

    /// <summary>
    ///     Gets the persistence operation(s) that raise <see cref="EventType" />.
    /// </summary>
    public EventOperations Operations { get; } = operations;

    /// <summary>
    ///     Gets the <see cref="EventOperations.Updated" /> narrowing property list. Empty means any
    ///     property change qualifies.
    /// </summary>
    public IReadOnlyList<string> Properties { get; } = properties;
}
