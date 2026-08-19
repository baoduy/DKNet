using Microsoft.Extensions.DependencyInjection;

namespace Fw.Extensions.Tests;

/// <summary>
///     Tests for <see cref="ServiceCollectionRegistrationExtensions" />'s duplicate-registration guard helpers,
///     used across DKNet packages to make repeated setup calls idempotent (DRK-466).
/// </summary>
public class ServiceCollectionRegistrationExtensionsTests
{
    #region Methods

    private interface IWidget;

    private class WidgetA : IWidget;

    private class WidgetB : IWidget;

    [Fact]
    public void IsRegistered_ServiceNotRegistered_ReturnsFalse()
    {
        var services = new ServiceCollection();

        services.IsRegistered<IWidget>().ShouldBeFalse();
    }

    [Fact]
    public void IsRegistered_ServiceRegistered_ReturnsTrue()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWidget, WidgetA>();

        services.IsRegistered<IWidget>().ShouldBeTrue();
    }

    [Fact]
    public void IsRegistered_RegardlessOfImplementation_StillReturnsTrue()
    {
        // IsRegistered is the first-wins guard: it must report "already registered" for any
        // implementation of TService, not just a specific one.
        var services = new ServiceCollection();
        services.AddSingleton<IWidget, WidgetA>();

        services.IsRegistered<IWidget>().ShouldBeTrue();
    }

    [Fact]
    public void IsRegisteredWithImplementation_SameServiceDifferentImplementation_ReturnsFalse()
    {
        // The exact-implementation guard must allow a second, different implementation of a
        // multi-implementation contract to coexist alongside the first.
        var services = new ServiceCollection();
        services.AddSingleton<IWidget, WidgetA>();

        services.IsRegisteredWithImplementation<IWidget>(typeof(WidgetB)).ShouldBeFalse();
    }

    [Fact]
    public void IsRegisteredWithImplementation_SameServiceSameImplementation_ReturnsTrue()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWidget, WidgetA>();

        services.IsRegisteredWithImplementation<IWidget>(typeof(WidgetA)).ShouldBeTrue();
    }

    [Fact]
    public void IsRegisteredWithImplementation_NothingRegistered_ReturnsFalse()
    {
        var services = new ServiceCollection();

        services.IsRegisteredWithImplementation<IWidget>(typeof(WidgetA)).ShouldBeFalse();
    }

    #endregion
}
