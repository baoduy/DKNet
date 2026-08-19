// ReSharper disable once CheckNamespace
// ReSharper disable MemberCanBePrivate.Global

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Provides extension methods for <see cref="IServiceCollection" /> to register services with keyed implementations.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="descriptor"></param>
    extension(ServiceDescriptor descriptor)
    {
        /// <summary>
        ///     Determines whether the specified <see cref="ServiceDescriptor" /> represents
        /// </summary>
        /// <param name="implementationType"></param>
        /// <returns></returns>
        public bool IsImplementationOf(Type implementationType) =>
            descriptor.ServiceType == implementationType || descriptor.ImplementationType == implementationType;

        /// <summary>
        ///     Determines whether the specified <see cref="ServiceDescriptor" /> represents
        /// </summary>
        /// <returns></returns>
        public bool IsImplementationOf<TImplement>() =>
            descriptor.IsImplementationOf(typeof(TImplement));

        /// <summary>
        ///     Determines whether the specified <see cref="ServiceDescriptor" /> represents a keyed service.
        /// </summary>
        /// <param name="keyName"></param>
        /// <param name="implementationType"></param>
        /// <returns></returns>
        public bool IsKeyedImplementationOf(object keyName,
            Type implementationType)
        {
            if (!descriptor.IsKeyedService || !ReferenceEquals(descriptor.ServiceKey, keyName))
                return false;
            return descriptor.IsImplementationOf(implementationType);
        }

        /// <summary>
        ///     Determines whether the specified <see cref="ServiceDescriptor" /> represents a keyed service.
        /// </summary>
        /// <param name="keyName"></param>
        /// <typeparam name="TImplement"></typeparam>
        /// <returns></returns>
        public bool IsKeyedImplementationOf<TImplement>(object keyName) =>
            descriptor.IsKeyedImplementationOf(keyName, typeof(TImplement));
    }
}

/// <summary>
///     Provides extension methods for <see cref="IServiceCollection" /> to guard against duplicate registrations.
/// </summary>
public static class ServiceCollectionRegistrationExtensions
{
    /// <param name="services"></param>
    extension(IServiceCollection services)
    {
        /// <summary>
        ///     Determines whether the specified <typeparamref name="TService" /> is already registered,
        ///     regardless of implementation. Use for first-wins guards on single-active contracts.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <returns></returns>
        public bool IsRegistered<TService>() =>
            services.Any(s => s.ServiceType == typeof(TService));

        /// <summary>
        ///     Determines whether the specified <typeparamref name="TService" /> is already registered with the
        ///     given <paramref name="implementationType" />. Use for exact-implementation guards on
        ///     multi-implementation contracts.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <param name="implementationType"></param>
        /// <returns></returns>
        public bool IsRegisteredWithImplementation<TService>(Type implementationType) =>
            services.Any(s => s.ServiceType == typeof(TService) && s.ImplementationType == implementationType);
    }
}