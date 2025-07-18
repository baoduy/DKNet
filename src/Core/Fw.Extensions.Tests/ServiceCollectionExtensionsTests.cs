using Microsoft.Extensions.DependencyInjection;

namespace Fw.Extensions.Tests;

public class ServiceCollectionExtensionMethodsTests
{
    private readonly ServiceCollection _services;

    public ServiceCollectionExtensionMethodsTests() => _services = [];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AsKeyedImplementedInterfacesThrowsArgumentExceptionForInvalidKeys(string key)
    {
        var types = new List<Type> { typeof(TestService) };

        Should.Throw<ArgumentException>(() =>
            _services.AsKeyedImplementedInterfaces(key, types));
    }

    [Fact]
    public void AsKeyedImplementedInterfacesThrowsArgumentNullExceptionWhenServicesNull()
    {
        IServiceCollection services = null;
        Should.Throw<ArgumentNullException>(() =>
            services.AsKeyedImplementedInterfaces("test", new List<Type>()));
    }

    [Fact]
    public void AsKeyedImplementedInterfacesThrowsArgumentNullExceptionWhenTypesNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            _services.AsKeyedImplementedInterfaces("test", null));
    }

    [Fact]
    public void AsKeyedImplementedInterfacesRegistersAllPublicInterfacesAndSelf()
    {
        var types = new List<Type> { typeof(TestService) };

        var result = _services.AsKeyedImplementedInterfaces("test", types);

        var descriptors = _services.ToList();
        descriptors.Count.ShouldBe(3);
        descriptors.ShouldContain(d =>
            d.ServiceType == typeof(ITestService) &&
            d.KeyedImplementationType == typeof(TestService));
        descriptors.ShouldContain(d =>
            d.ServiceType == typeof(IAnotherService) &&
            d.KeyedImplementationType == typeof(TestService));
        descriptors.ShouldContain(d =>
            d.ServiceType == typeof(TestService) &&
            d.KeyedImplementationType == typeof(TestService));
    }

    [Fact]
    public void AsKeyedImplementedInterfacesSkipsInterfaceTypes()
    {
        var types = new List<Type> { typeof(ISkipInterface) };

        _services.AsKeyedImplementedInterfaces("test", types);

        _services.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void AsKeyedImplementedInterfacesRespectsServiceLifetime(ServiceLifetime lifetime)
    {
        var types = new List<Type> { typeof(TestService) };

        _services.AsKeyedImplementedInterfaces("test", types, lifetime);

        _services.ShouldAllBe(d => d.Lifetime == lifetime);
    }

    [Fact]
    public void AsKeyedImplementedInterfacesReturnsSameInstanceForChaining()
    {
        var types = new List<Type> { typeof(TestService) };

        var result = _services.AsKeyedImplementedInterfaces("test", types);

        result.ShouldBeSameAs(_services);
    }

    [Fact]
    public void AsKeyedImplementedInterfacesWhenTypesEmptyNoRegistrationsAdded()
    {
        _services.AsKeyedImplementedInterfaces("test", new List<Type>());

        _services.Count.ShouldBe(0);
    }

    [Fact]
    public void AsKeyedImplementedInterfacesWithMultipleTypesRegistersAllImplementations()
    {
        var types = new List<Type> { typeof(TestService), typeof(AnotherTestService) };

        _services.AsKeyedImplementedInterfaces("test", types);

        _services.Count.ShouldBe(5); // 3 for TestService, 2 for AnotherTestService
    }

    [Fact]
    public void AsKeyedImplementedInterfacesWithDifferentKeysRegistersSeparately()
    {
        var types = new List<Type> { typeof(TestService) };

        _services.AsKeyedImplementedInterfaces("key1", types)
            .AsKeyedImplementedInterfaces("key2", types);

        _services.Count.ShouldBe(6); // 3 registrations per key
    }
}

public interface ITestService
{
}

public interface IAnotherService
{
}

public interface ISkipInterface
{
}

public class TestService : ITestService, IAnotherService
{
}

public class AnotherTestService : ITestService
{
}