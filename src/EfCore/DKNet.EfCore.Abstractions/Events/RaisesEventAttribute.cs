namespace DKNet.EfCore.Abstractions.Events;

/// <summary>
///     Declares that the carrying entity raises a generated event payload record for one or more
///     persistence operations after a successful save. Repeatable — apply once per event the entity raises.
/// </summary>
/// <remarks>
///     <para>
///     Two forms are supported. The type-naming form names an existing <c>[GenerateDto]</c> payload record:
///     <code>
///     [GenerateDto(typeof(Order), Exclude = new[] { "InternalNote" })]
///     public partial record OrderPlacedEvent;
///
///     [RaisesEvent(typeof(OrderPlacedEvent), EventOperations.Created)]
///     [RaisesEvent(typeof(OrderStatusChangedEvent), EventOperations.Updated, nameof(Order.Status))]
///     public class Order { public string Status { get; set; } = string.Empty; }
///     </code>
///     The string form names an event that does not exist yet: the build generates a public, partial,
///     default-shape payload record for it in the carrying entity's namespace — no hand-written
///     <c>[GenerateDto]</c> record is needed.
///     <code>
///     [RaisesEvent("CustomerTouched", EventOperations.Created)]
///     public class Customer { }
///     </code>
///     </para>
///     <para>
///     For the type-naming form, the named type must be a payload record generated via <c>[GenerateDto]</c>
///     from the SAME entity type this attribute is applied to; a build error is raised otherwise.
///     </para>
///     <para>
///     <c>properties</c> narrows an <see cref="EventOperations.Updated"/> rule: non-empty raises only when at
///     least one listed property changed (use <c>nameof(Entity.Property)</c> for compiler-checked names).
///     Empty raises on any change. Entries must be a direct property of the entity — nested paths are not
///     supported and fail the build. Narrowing on a rule whose <c>operations</c> has no
///     <see cref="EventOperations.Updated"/> flag is ignored at runtime and reported as a build warning.
///     </para>
///     <para>
///     This attribute alone does not raise anything: the application must register <c>DKNet.EfCore.Events</c>'
///     save hook (exactly as for hand-raised events) before declared events publish. A domain project that
///     only references <c>DKNet.EfCore.Abstractions</c> and <c>DKNet.EfCore.DtoGenerator</c> builds cleanly
///     with rules declared and simply never raises them until the application wires up the runtime.
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class RaisesEventAttribute : Attribute
{
    /// <summary>
    ///     Type-naming form: names an existing <c>[GenerateDto]</c> payload record to raise.
    /// </summary>
    /// <param name="eventType">The <c>[GenerateDto]</c>-generated payload record type to raise.</param>
    /// <param name="operations">The persistence operation(s) that raise <paramref name="eventType"/>.</param>
    /// <param name="properties">
    ///     For <see cref="EventOperations.Updated"/>, the narrowing property list. Empty means any change qualifies.
    /// </param>
    public RaisesEventAttribute(Type eventType, EventOperations operations, params string[] properties)
    {
        EventType = eventType;
        Operations = operations;
        Properties = properties;
    }

    /// <summary>
    ///     String form: names an event by string with no hand-written payload record. The build generates a
    ///     public, partial, default-shape payload record for <paramref name="eventName"/> in the carrying
    ///     entity's namespace.
    /// </summary>
    /// <param name="eventName">The name of the event to raise; also the generated record's type name.</param>
    /// <param name="operations">The persistence operation(s) that raise the named event.</param>
    /// <param name="properties">
    ///     For <see cref="EventOperations.Updated"/>, the narrowing property list. Empty means any change qualifies.
    /// </param>
    public RaisesEventAttribute(string eventName, EventOperations operations, params string[] properties)
    {
        EventName = eventName;
        Operations = operations;
        Properties = properties;
    }

    /// <summary>
    ///     Gets the <c>[GenerateDto]</c>-generated payload record type to raise (type-naming form).
    ///     <see langword="null"/> for the string form; exactly one of <see cref="EventType"/>/<see cref="EventName"/>
    ///     is set per instance.
    /// </summary>
    public Type? EventType { get; }

    /// <summary>
    ///     Gets the event name to raise (string form). <see langword="null"/> for the type-naming form; exactly
    ///     one of <see cref="EventType"/>/<see cref="EventName"/> is set per instance.
    /// </summary>
    public string? EventName { get; }

    /// <summary>
    ///     Gets the persistence operation(s) that raise the declared event.
    /// </summary>
    public EventOperations Operations { get; }

    /// <summary>
    ///     Gets the <see cref="EventOperations.Updated" /> narrowing property list. Empty means any
    ///     property change qualifies.
    /// </summary>
    public IReadOnlyList<string> Properties { get; }
}
