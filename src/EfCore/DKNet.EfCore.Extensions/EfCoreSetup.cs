﻿using DKNet.EfCore.Extensions.Configurations;
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
    public static IServiceCollection AddGlobalModelBuilderRegister<TImplementation>(this IServiceCollection services)
        where TImplementation : class, IGlobalQueryFilterRegister
    {
        GlobalQueryFilters.Add(typeof(TImplementation));
        return services;
    }

    /// <summary>
    ///     Scan and register all Entities from assemblies to DbContext.
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    /// <param name="this"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static DbContextOptionsBuilder<TContext> UseAutoConfigModel<TContext>(
        this DbContextOptionsBuilder<TContext> @this, Action<IEntityAutoConfigRegister>? options = null)
        where TContext : DbContext =>
        (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)@this)
        .UseAutoConfigModel(options);

    /// <summary>
    ///     Scan and register all Entities from assemblies to DbContext.
    /// </summary>
    /// <param name="this"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static DbContextOptionsBuilder UseAutoConfigModel(this DbContextOptionsBuilder @this,
        Action<IEntityAutoConfigRegister>? options = null)
    {
        ArgumentNullException.ThrowIfNull(@this);

        var op = @this.GetOrCreateExtension();
        options?.Invoke(op);

        return @this;
    }

    /// <summary>
    ///     Get or Create the EntityMappingRegister from the DbContextOptionsBuilder.
    /// </summary>
    /// <param name="optionsBuilder"></param>
    /// <returns></returns>
    internal static EntityAutoConfigRegister GetOrCreateExtension(this DbContextOptionsBuilder optionsBuilder)
    {
        var op = optionsBuilder.Options.FindExtension<EntityAutoConfigRegister>();
        if (op != null) return op;

        op = new EntityAutoConfigRegister();
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(op);

        return op;
    }
}