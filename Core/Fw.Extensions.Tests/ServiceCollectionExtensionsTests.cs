using Microsoft.Extensions.DependencyInjection;

namespace Fw.Extensions.Tests
{
    [TestClass]
    public class ServiceCollectionExtensionMethodsTests
    {
        private ServiceCollection _services;

        [TestInitialize]
        public void Initialize()
        {
            _services = new ServiceCollection();
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void AsKeyedImplementedInterfacesThrowsArgumentExceptionForInvalidKeys(string key)
        {
            var types = new List<Type> { typeof(TestService) };

            Assert.ThrowsException<ArgumentException>(() =>
                _services.AsKeyedImplementedInterfaces(key, types));
        }

        [TestMethod]
        public void AsKeyedImplementedInterfacesThrowsArgumentNullExceptionWhenServicesNull()
        {
            IServiceCollection services = null;
            Assert.ThrowsException<ArgumentNullException>(() =>
                services.AsKeyedImplementedInterfaces("test", new List<Type>()));
        }

        [TestMethod]
        public void AsKeyedImplementedInterfacesThrowsArgumentNullExceptionWhenTypesNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                _services.AsKeyedImplementedInterfaces("test", types: null));
        }

        [TestMethod]
        public void AsKeyedImplementedInterfacesRegistersAllPublicInterfacesAndSelf()
        {
            var types = new List<Type> { typeof(TestService) };

            var result = _services.AsKeyedImplementedInterfaces("test", types);

            var descriptors = _services.ToList();
            Assert.AreEqual(3, descriptors.Count);
            Assert.IsTrue(descriptors.Any(d =>
                d.ServiceType == typeof(ITestService) &&
                d.KeyedImplementationType == typeof(TestService)));
            Assert.IsTrue(descriptors.Any(d =>
                d.ServiceType == typeof(IAnotherService) &&
                d.KeyedImplementationType == typeof(TestService)));
            Assert.IsTrue(descriptors.Any(d =>
                d.ServiceType == typeof(TestService) &&
                d.KeyedImplementationType == typeof(TestService)));
        }

        [TestMethod]
        public void AsKeyedImplementedInterfacesSkipsInterfaceTypes()
        {
            var types = new List<Type> { typeof(ISkipInterface) };

            _services.AsKeyedImplementedInterfaces("test", types);

            Assert.AreEqual(0, _services.Count);
        }

        [TestMethod]
        [DataRow(ServiceLifetime.Singleton)]
        [DataRow(ServiceLifetime.Scoped)]
        [DataRow(ServiceLifetime.Transient)]
        public void AsKeyedImplementedInterfacesRespectsServiceLifetime(ServiceLifetime lifetime)
        {
            var types = new List<Type> { typeof(TestService) };

            _services.AsKeyedImplementedInterfaces("test", types, lifetime);

            Assert.IsTrue(_services.All(d => d.Lifetime == lifetime));
        }

        [TestMethod]
        public void AsKeyedImplementedInterfacesReturnsSameInstanceForChaining()
        {
            var types = new List<Type> { typeof(TestService) };

            var result = _services.AsKeyedImplementedInterfaces("test", types);

            Assert.AreSame(_services, result);
        }

        [TestMethod]
        public void AsKeyedImplementedInterfacesWhenTypesEmptyNoRegistrationsAdded()
        {
            _services.AsKeyedImplementedInterfaces("test", new List<Type>());

            Assert.AreEqual(0, _services.Count);
        }

        [TestMethod]
        public void AsKeyedImplementedInterfacesWithMultipleTypesRegistersAllImplementations()
        {
            var types = new List<Type> { typeof(TestService), typeof(AnotherTestService) };

            _services.AsKeyedImplementedInterfaces("test", types);

            Assert.AreEqual(5, _services.Count); // 3 for TestService, 2 for AnotherTestService
        }

        [TestMethod]
        public void AsKeyedImplementedInterfacesWithDifferentKeysRegistersSeparately()
        {
            var types = new List<Type> { typeof(TestService) };

            _services.AsKeyedImplementedInterfaces("key1", types)
                    .AsKeyedImplementedInterfaces("key2", types);

            Assert.AreEqual(6, _services.Count); // 3 registrations per key
        }
    }

    public interface ITestService { }
    public interface IAnotherService { }
    public interface ISkipInterface { }

    public class TestService : ITestService, IAnotherService { }

    public class AnotherTestService : ITestService { }
}