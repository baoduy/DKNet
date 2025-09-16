using System.Diagnostics;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Extensions.Configurations;
using DKNet.Fw.Extensions;
using DKNet.Fw.Extensions.TypeExtractors;

// ReSharper disable CheckNamespace
namespace Microsoft.EntityFrameworkCore;

[SuppressMessage("Major Code Smell",
    "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields")]
internal static class EntityAutoConfigRegisterExtensions
{
    private static readonly MethodInfo RegisterMappingMethod = typeof(EntityAutoConfigRegisterExtensions)
        .GetMethod(nameof(RegisterMapping), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly Type DefaultEntityTypeConfiguration = typeof(DefaultEntityTypeConfiguration<>);
    internal static readonly Type InterfaceEntityTypeConfiguration = typeof(IEntityTypeConfiguration<>);

    /// <summary>
    ///     Register EntityTypeConfiguration from RegistrationInfos <see cref="AutoEntityRegistrationInfo" />
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="registrations">The registration information.</param>
    internal static void RegisterEntityMappingFrom(this ModelBuilder modelBuilder,
        IEnumerable<AutoEntityRegistrationInfo> registrations)
    {
        foreach (var type in registrations)
            modelBuilder.RegisterEntityMappingFrom(type);
    }

    /// <summary>
    ///     Register GlobalFilter from RegistrationInfos <see cref="AutoEntityRegistrationInfo" />
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="registrations">The registration information.</param>
    /// <param name="dbContext">The database context.</param>
    internal static void RegisterGlobalFilterFrom(this ModelBuilder modelBuilder,
        IEnumerable<AutoEntityRegistrationInfo> registrations, DbContext dbContext)
    {
        foreach (var info in registrations)
            modelBuilder.RegisterGlobalFilterFrom(info, dbContext);
    }

    /// <summary>
    ///     Get all Entity Classes and Abstract class without Interface or Generic
    /// </summary>
    /// <param name="registration">The registration information.</param>
    /// <returns>An enumerable of entity types.</returns>
    internal static IEnumerable<Type> GetAllEntityTypes(this AutoEntityRegistrationInfo registration)
    {
        return registration.EntityAssemblies.Extract().Classes().NotGeneric().IsInstanceOf(typeof(IEntity<>))
            .Where(t => !t.HasAttribute<IgnoreEntityAttribute>(true));
    }

    /// <summary>
    ///     Get All IEntityTypeConfiguration for entities.
    /// </summary>
    /// <param name="registration">The registration information.</param>
    /// <returns>An enumerable of defined mapper types.</returns>
    internal static IEnumerable<Type> GetDefinedMappers(this AutoEntityRegistrationInfo registration) =>
        registration.EntityAssemblies.Extract().Classes().NotAbstract().NotGeneric()
            .IsInstanceOf(InterfaceEntityTypeConfiguration);

    /// <summary>
    ///     Get All generic IEntityTypeConfiguration that can be used for the un-mapped entities.
    /// </summary>
    /// <param name="registration">The registration information.</param>
    /// <returns>An enumerable of generic mapper types.</returns>
    internal static IEnumerable<Type> GetGenericMappers(this AutoEntityRegistrationInfo registration) =>
        registration.EntityAssemblies.Extract().Classes().NotAbstract().Generic()
            .IsInstanceOf(InterfaceEntityTypeConfiguration);

    /// <summary>
    ///     Scan GlobalFilter from Assemblies
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>An enumerable of global filter types.</returns>
    private static IEnumerable<Type> ScanGlobalFilterFrom(this ICollection<Assembly> assemblies) =>
        assemblies.Extract().Classes().NotAbstract().IsInstanceOf<IGlobalQueryFilterRegister>();

    /// <summary>
    ///     Register GlobalFilter from RegistrationInfo <see cref="AutoEntityRegistrationInfo" />
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="info">The registration information.</param>
    /// <param name="dbContext">The database context.</param>
    private static void RegisterGlobalFilterFrom(this ModelBuilder modelBuilder, AutoEntityRegistrationInfo info,
        DbContext dbContext)
    {
        var globalFilters = info.EntityAssemblies.ScanGlobalFilterFrom().Union(EfCoreSetup.GlobalQueryFilters)
            .Distinct();
        foreach (var filter in globalFilters)
        {
            var filterInstance = Activator.CreateInstance(filter) as IGlobalQueryFilterRegister;
            filterInstance?.Apply(modelBuilder, dbContext);
        }
    }

    /// <summary>
    ///     Check if the type is a generic type of another type.
    /// </summary>
    /// <param name="genericType">The generic type.</param>
    /// <param name="type">The type to check against.</param>
    /// <returns>True if the type is a generic type of the specified generic type; otherwise, false.</returns>
    private static bool IsGenericTypeOf(this Type genericType, Type type)
    {
        return genericType.GetGenericParameterConstraints().Any(c => c.IsAssignableFrom(type))
               || genericType.IsAssignableFrom(type)
               || genericType.BaseType?.IsAssignableFrom(type) == true;
    }

    /// <summary>
    ///     Create a Mapper for an entity type. The first generic type that matches with entity type condition will be
    ///     selected.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="mapperTypes">The list of generic types.</param>
    /// <returns>The created mapper type.</returns>
    private static Type CreateMapperFromGeneric(Type entityType, IEnumerable<Type> mapperTypes)
    {
        foreach (var mapperType in mapperTypes)
        {
            // The generic type should have 1 GenericTypeParameters only
            var genericType = mapperType.GetTypeInfo().GenericTypeParameters.Single();
            if (!genericType.IsGenericTypeOf(entityType)) continue;

            Trace.TraceInformation($"Auto Created Entity Config Mapper For: {entityType.Name} using {mapperType.Name}");
            return mapperType.MakeGenericType(entityType);
        }

        throw new ArgumentException(
            $"There is no {typeof(IEntityTypeConfiguration<>).Name} found for {entityType.Name}", nameof(entityType));
    }

    /// <summary>
    ///     Register entity mapping from registration information.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="registration">The registration information.</param>
    private static void RegisterEntityMappingFrom(this ModelBuilder modelBuilder,
        AutoEntityRegistrationInfo registration)
    {
        if (registration.DefaultEntityMapperTypes == null)
            registration.WithDefaultMappersType(DefaultEntityTypeConfiguration);

        var allDefinedMappers = registration.GetDefinedMappers().ToList();
        var entityTypes = registration.GetAllEntityTypes().ToList();

        Trace.TraceInformation($"Auto Found entities: {string.Join('\n', entityTypes.Select(t => t.Name))}");

        var requiredEntityTypes = registration.Predicate == null
            ? entityTypes
            : [.. entityTypes.Where(registration.Predicate.Compile())];

        var genericMappers =
            registration.GetGenericMappers().Concat(registration.DefaultEntityMapperTypes!).ToList();

        Trace.TraceInformation(
            $"Auto Found Generic configuration: {string.Join('\n', genericMappers.Select(t => t.Name))}");

        // Map Entities to ModelBuilder
        foreach (var entityType in requiredEntityTypes)
        {
            var mapper = allDefinedMappers.FirstOrDefault(m =>
                m.BaseType?.GenericTypeArguments.FirstOrDefault() == entityType);

            if (mapper == null && !entityType.IsAbstract)
                mapper = CreateMapperFromGeneric(entityType, genericMappers);

            if (mapper != null)
            {
                modelBuilder.RegisterMappingFromType(mapper);
                // Remove Added from the list
                allDefinedMappers.Remove(mapper);
            }
        }

        // Add remaining to ModelBuilder
        foreach (var type in allDefinedMappers) modelBuilder.RegisterMappingFromType(type);
    }

    /// <summary>
    ///     Generic RegisterMapping.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TMapping">The mapping type.</typeparam>
    /// <param name="builder">The model builder.</param>
    /// <returns>The model builder.</returns>
    private static ModelBuilder RegisterMapping<TEntity, TMapping>(this ModelBuilder builder)
        where TMapping : IEntityTypeConfiguration<TEntity>
        where TEntity : class
    {
        var mapper = Activator.CreateInstance<TMapping>();
        builder.ApplyConfiguration(mapper);
        return builder;
    }

    /// <summary>
    ///     Register mapping from type.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="mapperType">The mapper type.</param>
    private static void RegisterMappingFromType(this ModelBuilder modelBuilder, Type mapperType)
    {
        ArgumentNullException.ThrowIfNull(mapperType);

        var eType = EfCoreExtensions.GetEntityType(mapperType);

        if (RegisterMappingMethod == null || eType == null)
            throw new InvalidOperationException($"The {nameof(RegisterMapping)} or EntityType are not found");

        var md = RegisterMappingMethod.MakeGenericMethod(eType, mapperType);
        md.Invoke(null, [modelBuilder]);
    }
}