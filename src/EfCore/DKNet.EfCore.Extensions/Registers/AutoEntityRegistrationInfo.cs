using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.Extensions.Registers;

public sealed class AutoEntityRegistrationInfo
{
    internal AutoEntityRegistrationInfo(params Assembly[] entityAssemblies)
    {
        if (entityAssemblies.Length <= 0)
            throw new ArgumentNullException(nameof(entityAssemblies));

        EntityAssemblies = entityAssemblies;
    }

    internal Type[]? DefaultEntityMapperTypes { get; private set; }

    internal ICollection<Assembly> EntityAssemblies { get; }

    internal Expression<Func<Type, bool>>? Predicate { get; private set; }

    /// <summary>
    /// Sets the default mapper types for the entities. These mapper types must be generic and implement <see cref="IEntityTypeConfiguration{TEntity}"/>.
    /// </summary>
    /// <param name="entityMapperTypes">The default mapper types.</param>
    /// <returns>The current <see cref="AutoEntityRegistrationInfo"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if any of the provided types are not generic or do not implement <see cref="IEntityTypeConfiguration{TEntity}"/>.</exception>
    public AutoEntityRegistrationInfo WithDefaultMappersType(params Type[] entityMapperTypes)
    {
        if (entityMapperTypes == null || entityMapperTypes.Length == 0)
            throw new ArgumentNullException(nameof(entityMapperTypes));

        if (!entityMapperTypes.All(t => t.IsGenericType))
            throw new InvalidOperationException($"The {nameof(DefaultEntityMapperTypes)} must be a Generic Type.");

        if (!entityMapperTypes.All(t => t.GetInterfaces().Any(y =>
                y.IsGenericType && y.GetGenericTypeDefinition() == EntityAutoConfigRegisterExtensions.InterfaceEntityTypeConfiguration)))
            throw new InvalidOperationException(
                $"The {nameof(DefaultEntityMapperTypes)} must be an instance of {EntityAutoConfigRegisterExtensions.InterfaceEntityTypeConfiguration.Name}.");

        DefaultEntityMapperTypes = entityMapperTypes;
        return this;
    }

    /// <summary>
    /// Sets a filter predicate to determine which types should be registered.
    /// </summary>
    /// <param name="predicate">The filter predicate.</param>
    /// <returns>The current <see cref="AutoEntityRegistrationInfo"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the provided predicate is null.</exception>
    public AutoEntityRegistrationInfo WithFilter(Expression<Func<Type, bool>> predicate)
    {
        Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        return this;
    }

    /// <summary>
    /// Validates the current configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if any of the default entity mapper types are not generic or do not implement <see cref="IEntityTypeConfiguration{TEntity}"/>.</exception>
    internal void Validate()
    {
        if (DefaultEntityMapperTypes == null) return;

        if (!DefaultEntityMapperTypes.All(t => t.IsGenericType))
            throw new InvalidOperationException($"The {nameof(DefaultEntityMapperTypes)} must be a Generic Type.");

        if (!DefaultEntityMapperTypes.All(t => t.GetInterfaces().Any(y =>
                y.IsGenericType && y.GetGenericTypeDefinition() == EntityAutoConfigRegisterExtensions.InterfaceEntityTypeConfiguration)))
            throw new InvalidOperationException(
                $"The {nameof(DefaultEntityMapperTypes)} must be an instance of {EntityAutoConfigRegisterExtensions.InterfaceEntityTypeConfiguration.Name}.");
    }
}