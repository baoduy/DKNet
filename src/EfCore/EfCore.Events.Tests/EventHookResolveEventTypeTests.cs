using System.Reflection;
using DKNet.EfCore.Abstractions.Events;
using DKNet.EfCore.Events;
using DKNet.EfCore.Events.Internals;

namespace EfCore.Events.Tests;

/// <summary>
///     DRK-471 §3 loud-failure invariant — a string-form <c>[RaisesEvent]</c> rule whose generated payload
///     record cannot be found (the domain project references <c>DKNet.EfCore.Abstractions</c> and
///     <c>DKNet.EfCore.Events</c> but not the generator, or simply typo'd the name) must fail loudly at
///     save time, never silently drop the event. <see cref="EventHook" />'s resolution runs inside
///     <c>BeforeSaveAsync</c> — before EF Core issues any SQL — so proving it throws here proves no save
///     completes with the event silently dropped, without needing a real "generator not referenced" project.
/// </summary>
public class EventHookResolveEventTypeTests
{
    #region Methods

    [Fact]
    public void ResolveEventType_StringFormRuleWithNoGeneratedPayload_ThrowsNamingTheMissingEvent()
    {
        // Arrange - a well-formed string-form rule naming an event no generated record exists for
        // (exactly what happens when DKNet.EfCore.DtoGenerator isn't referenced by the domain project).
        var rule = new RaisesEventAttribute("GhostEventThatWasNeverGenerated", EventOperations.Created);
        var resolveEventType = typeof(EventHook).GetMethod(
            "ResolveEventType", BindingFlags.NonPublic | BindingFlags.Static);
        resolveEventType.ShouldNotBeNull();

        // Act
        var invoke = () => resolveEventType.Invoke(null, [typeof(Product), rule]);

        // Assert
        var wrapper = Should.Throw<TargetInvocationException>(invoke);
        var thrown = wrapper.InnerException.ShouldBeOfType<EventException>();
        thrown.Message.ShouldContain("GhostEventThatWasNeverGenerated");
        thrown.Message.ShouldContain(nameof(Product));
    }

    #endregion
}
