using System;
using System.Linq.Expressions;
using System.Reflection;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     The DI setup for Infra
/// </summary>
public static class InternalSetup
{
    /// <summary>
    ///     Add Boundary Context with DomainServices
    /// </summary>
    /// <typeparam name="TContext">The BondedContext from <see cref="DbContext" /> </typeparam>
    /// <param name="service"></param>
    /// <param name="contextBuilder"></param>
    /// <param name="assembliesToScans">
    ///     The assemblies to scan all Entities, Configuration The TContext assembly will be use if
    ///     not provided.
    /// </param>
    /// <param name="entityFilter"></param>
    /// <returns></returns>
    internal static IServiceCollection AddBoundedContext<TContext>(this IServiceCollection service,
        Action<DbContextOptionsBuilder> contextBuilder,
        Assembly[] assembliesToScans = null,
        Expression<Func<Type, bool>> entityFilter = null) where TContext : DbContext
    {
        assembliesToScans ??= [typeof(TContext).Assembly];

        void OptionFunc(DbContextOptionsBuilder op)
        {
            contextBuilder(op);
            op.UseAutoConfigModel(o =>
            {
                var scan = o.ScanFrom(assembliesToScans);
                if (entityFilter != null) scan.WithFilter(entityFilter);//.IgnoreOthers();
            });
        }
            
        service
            .AddDbContextWithHook<TContext>(OptionFunc);

        return service;
    }

    /// <summary>
    ///     The wrapper method to add all needed components for infra
    ///     - Add Scan Event Handlers from Assemblies />
    ///     - Call <see cref="AddBoundedContext{TContext}" />
    ///     - Call Automapper registration.
    /// </summary>
    /// <param name="service"></param>
    /// <param name="contextBuilder"></param>
    /// <param name="assembliesToScans"></param>
    /// <param name="entityFilter">
    ///     Customize entity scanning filtering. Default all classes that inherited IEntity will be
    ///     included. This is useful when you want to filter out the difference entities that belong different DbContext.
    /// </param>
    /// <typeparam name="TContext"></typeparam>
    /// <returns></returns>
    public static IServiceCollection AddCoreInfraServices<TContext>(this IServiceCollection service,
        Action<DbContextOptionsBuilder> contextBuilder,
        Assembly[] assembliesToScans = null,
        Expression<Func<Type, bool>> entityFilter = null) where TContext : DbContext
    {
        assembliesToScans ??= [typeof(TContext).Assembly];

        return service.AddBoundedContext<TContext>(contextBuilder,
            assembliesToScans,
            entityFilter);
    }
}