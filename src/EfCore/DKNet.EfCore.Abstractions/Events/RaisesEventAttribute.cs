namespace DKNet.EfCore.Abstractions.Events;

/// <summary>
///     Declares that the carrying entity raises a generated event payload record for one or more
///     persistence operations after a successful save. Repeatable — apply once per event the entity raises.
/// </summary>
/// <remarks>
///     <para>
///     Three forms are supported. The type-naming form names an existing <c>[GenerateDto]</c> payload record:
///     <code>
///     [GenerateDto(typeof(Order), Exclude = new[] { "InternalNote" })]
///     public partial record OrderPlacedEvent;
///
///     [RaisesEvent(typeof(OrderPlacedEvent), EventOperations.Created)]
///     [RaisesEvent(typeof(OrderStatusChangedEvent), EventOperations.Updated, nameof(Order.Status))]
///     public class Order { public string Status { get; set; } = string.Empty; }
///     </code>
///     The convention forms name no event at all: the payload record's name is composed by fixed convention
///     — entity name, optional label, narrowing properties, operations, then <c>Event</c> — and the build
///     generates a public, partial, default-shape payload record for it in the carrying entity's namespace,
///     no hand-written <c>[GenerateDto]</c> record needed.
///     <code>
///     [RaisesEvent(EventOperations.Created)]
///     public class Customer { }
///     // generates CustomerCreatedEvent
///     </code>
///     The label form adds a fixed word into the composed name, ahead of any narrowing properties:
///     <code>
///     [RaisesEvent("Touched", EventOperations.Created)]
///     public class Customer { }
///     // generates CustomerTouchedCreatedEvent
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
    ///     Label convention form: composes the generated record's name from the entity name, this label,
    ///     any narrowing properties, and the declared operations. See the type remarks for the composition
    ///     order.
    /// </summary>
    /// <param name="label">The label segment to compose into the generated record's name.</param>
    /// <param name="operations">The persistence operation(s) that raise the composed event.</param>
    /// <param name="properties">
    ///     For <see cref="EventOperations.Updated"/>, the narrowing property list. Empty means any change qualifies.
    /// </param>
    public RaisesEventAttribute(string label, EventOperations operations, params string[] properties)
    {
        Label = label;
        Operations = operations;
        Properties = properties;
    }

    /// <summary>
    ///     Label-less convention form: composes the generated record's name from the entity name, any
    ///     narrowing properties, and the declared operations — no label segment. See the type remarks for
    ///     the composition order.
    /// </summary>
    /// <param name="operations">The persistence operation(s) that raise the composed event.</param>
    /// <param name="properties">
    ///     For <see cref="EventOperations.Updated"/>, the narrowing property list. Empty means any change qualifies.
    /// </param>
    public RaisesEventAttribute(EventOperations operations, params string[] properties)
    {
        Operations = operations;
        Properties = properties;
    }

    /// <summary>
    ///     Gets the <c>[GenerateDto]</c>-generated payload record type to raise (type-naming form).
    ///     <see langword="null"/> for the convention forms; exactly one of <see cref="EventType"/>/<see cref="Label"/>
    ///     is set per instance, and neither is set for the label-less convention form.
    /// </summary>
    public Type? EventType { get; }

    /// <summary>
    ///     Gets the label segment composed into the generated record's name (label convention form).
    ///     <see langword="null"/> for the type-naming and label-less convention forms.
    /// </summary>
    public string? Label { get; }

    /// <summary>
    ///     Gets the persistence operation(s) that raise the declared event.
    /// </summary>
    public EventOperations Operations { get; }

    /// <summary>
    ///     Gets the <see cref="EventOperations.Updated" /> narrowing property list. Empty means any
    ///     property change qualifies.
    /// </summary>
    public IReadOnlyList<string> Properties { get; }

    /// <summary>
    ///     Gets or sets the properties to exclude from the AUTOMATICALLY COMPOSED payload (convention forms
    ///     only). Mutually exclusive with <see cref="Include"/> — specifying both is a build error. Takes no
    ///     part in event-name composition (use the label to distinguish two events on one entity). Supplying
    ///     this on the type-naming form is a build error — that form's payload record owns its own shape via
    ///     its own <c>[GenerateDto]</c> <c>Exclude</c>/<c>Include</c>.
    /// </summary>
    public string[] Exclude { get; set; } = [];

    /// <summary>
    ///     Gets or sets the only properties to keep in the AUTOMATICALLY COMPOSED payload (convention forms
    ///     only). Mutually exclusive with <see cref="Exclude"/> — specifying both is a build error. A
    ///     non-empty value is the whole truth for the payload shape and overrides the project-wide
    ///     <c>DtoGeneratorExclusions</c> list. Takes no part in event-name composition (use the label to
    ///     distinguish two events on one entity). Supplying this on the type-naming form is a build error —
    ///     that form's payload record owns its own shape via its own <c>[GenerateDto]</c>
    ///     <c>Exclude</c>/<c>Include</c>.
    /// </summary>
    public string[] Include { get; set; } = [];
}
