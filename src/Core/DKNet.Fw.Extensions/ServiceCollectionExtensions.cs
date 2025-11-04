// ReSharper disable once CheckNamespace
// ReSharper disable MemberCanBePrivate.Global

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Provides extension methods for <see cref="IServiceCollection" /> to register services with keyed implementations.
/// </summary>
public static class ServiceCollectionExtensions
{
    #region Methods

    /// <summary>
    ///     Determines whether the specified <see cref="ServiceDescriptor" /> represents
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="implementationType"></param>
    /// <returns></returns>
    public static bool IsImplementationOf(this ServiceDescriptor descriptor, Type implementationType) =>
        descriptor.ServiceType == implementationType || descriptor.ImplementationType == implementationType;

    /// <summary>
    ///     Determines whether the specified <see cref="ServiceDescriptor" /> represents
    /// </summary>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    public static bool IsImplementationOf<TImplement>(this ServiceDescriptor descriptor) =>
        descriptor.IsImplementationOf(typeof(TImplement));

    /// <summary>
    ///     Determines whether the specified <see cref="ServiceDescriptor" /> represents a keyed service.
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="keyName"></param>
    /// <param name="implementationType"></param>
    /// <returns></returns>
    public static bool IsKeyedImplementationOf(this ServiceDescriptor descriptor, object keyName,
        Type implementationType)
    {
        if (!descriptor.IsKeyedService || !ReferenceEquals(descriptor.ServiceKey, keyName))
            return false;
        return descriptor.IsImplementationOf(implementationType);
    }

    /// <summary>
    ///     Determines whether the specified <see cref="ServiceDescriptor" /> represents a keyed service.
    /// </summary>
    /// <param name="descriptor"></param>
    /// <param name="keyName"></param>
    /// <typeparam name="TImplement"></typeparam>
    /// <returns></returns>
    public static bool IsKeyedImplementationOf<TImplement>(this ServiceDescriptor descriptor, object keyName) =>
        descriptor.IsKeyedImplementationOf(keyName, typeof(TImplement));

    #endregion
}