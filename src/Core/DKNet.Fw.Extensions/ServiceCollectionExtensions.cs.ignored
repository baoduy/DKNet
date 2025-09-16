// ReSharper disable once CheckNamespace

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Provides extension methods for <see cref="IServiceCollection" /> to register services with keyed implementations.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers multiple implementations of interfaces with a specified key.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
    /// <param name="key">The key to associate with the registered services.</param>
    /// <param name="types">A collection of <see cref="Type" /> objects representing the implementations to register.</param>
    /// <param name="lifetime">
    ///     The <see cref="ServiceLifetime" /> for the registered services (default is
    ///     <see cref="ServiceLifetime.Scoped" />).
    /// </param>
    /// <returns>The modified <see cref="IServiceCollection" />.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown if <paramref name="services" /> or <paramref name="types" /> is
    ///     <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key" /> is <c>null</c> or whitespace.</exception>
    [UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2075",
        Justification = "Everything referenced in the loaded assembly is manually preserved, so it's safe")]
    [UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2072",
        Justification = "Everything referenced in the loaded assembly is manually preserved, so it's safe")]
    public static IServiceCollection AsKeyedImplementedInterfaces(this IServiceCollection services, string key,
        IEnumerable<Type> types, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(types);

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or whitespace", nameof(key));

        foreach (var classType in types)
        {
            if (classType.IsInterface)
                continue;

            var interfaces = classType.GetInterfaces()
                .Where(i => i != typeof(IDisposable) && i.IsPublic);

            // Add interfaces
            foreach (var i in interfaces)
                services.Add(new ServiceDescriptor(i, key, classType, lifetime));

            //Add itself
            services.Add(new ServiceDescriptor(classType, key, classType, lifetime));
        }

        return services;
    }
}