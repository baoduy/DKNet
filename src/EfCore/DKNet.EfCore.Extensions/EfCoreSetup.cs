using DKNet.EfCore.Extensions.Configurations;
using DKNet.EfCore.Extensions.Internal;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable CheckNamespace
namespace Microsoft.EntityFrameworkCore;

public static class EfCoreSetup
{
    internal static readonly HashSet<Type> GlobalQueryFilters = [];

    /// <summary>
    ///     Register the GlobalModelBuilderRegister to the service collection.
    /// </summary>
    /// <typeparam name="TImplementation"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddGlobalQueryFilter<TImplementation>(this IServiceCollection services)
        where TImplementation : class, IGlobalModelBuilder
    {
        GlobalQueryFilters.Add(typeof(TImplementation));
        return services;
    }

    /// <summary>
    ///     Scan and register all Entities from assemblies to DbContext.
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    /// <param name="this"></param>
    /// <param name="assemblies"></param>
    /// <returns></returns>
    public static DbContextOptionsBuilder<TContext> UseAutoConfigModel<TContext>(
        this DbContextOptionsBuilder<TContext> @this, params Assembly[]? assemblies)
        where TContext : DbContext =>
        (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)@this)
        .UseAutoConfigModel(assemblies is null || assemblies.Length <= 0 ? [typeof(TContext).Assembly] : assemblies);

    /// <summary>
    ///     Scan and register all Entities from assemblies to DbContext.
    /// </summary>
    /// <param name="this"></param>
    /// <param name="assemblies"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static DbContextOptionsBuilder UseAutoConfigModel(this DbContextOptionsBuilder @this, Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(@this);
        @this.GetOrCreateExtension(assemblies);
        return @this;
    }

    /// <summary>
    ///     Get or Create the EntityMappingRegister from the DbContextOptionsBuilder.
    /// </summary>
    /// <param name="optionsBuilder"></param>
    /// <param name="assemblies"></param>
    /// <returns></returns>
    private static EntityAutoConfigRegister GetOrCreateExtension(this DbContextOptionsBuilder optionsBuilder,
        Assembly[] assemblies)
    {
        var op = optionsBuilder.Options.FindExtension<EntityAutoConfigRegister>();
        if (op != null) return op;

        op = new EntityAutoConfigRegister(assemblies);
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(op);

        return op;
    }
}