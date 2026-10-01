using System.Collections.Concurrent;
using DKNet.EfCore.Extensions.Configurations;
using DKNet.EfCore.Extensions.Extensions;
using DKNet.EfCore.Extensions.Internal;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable CheckNamespace
namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// </summary>
public static class EfCoreSetup
{
    #region Methods

    /// <summary>
    ///     Get or Create the EntityMappingRegister from the DbContextOptionsBuilder.
    /// </summary>
    /// <param name="optionsBuilder"></param>
    /// <param name="assemblies"></param>
    /// <returns></returns>
    private static EntityAutoConfigRegister GetOrCreateExtension(
        this DbContextOptionsBuilder optionsBuilder,
        Assembly[] assemblies)
    {
        var op = optionsBuilder.Options.FindExtension<EntityAutoConfigRegister>();
        if (op != null) return op;

        op = new EntityAutoConfigRegister(assemblies);
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(op);

        return op;
    }

    /// <summary>
    ///     Scan and register all Entities from assemblies to DbContext.
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    /// <param name="this"></param>
    /// <param name="assemblies"></param>
    /// <returns></returns>
    public static DbContextOptionsBuilder<TContext> UseAutoConfigModel<TContext>(
        this DbContextOptionsBuilder<TContext> @this,
        params Assembly[]? assemblies)
        where TContext : DbContext =>
        (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)@this)
        .UseAutoConfigModel(assemblies is { Length: > 0 } ? assemblies : [typeof(TContext).Assembly]);

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

    #endregion

    /// <param name="serviceCollection"></param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        ///     Registers a custom EF Core exception handler for the specified <typeparamref name="TDbContext" />.
        /// </summary>
        /// <typeparam name="TDbContext"></typeparam>
        /// <typeparam name="TExceptionHandler"></typeparam>
        /// <returns></returns>
        public IServiceCollection AddEfCoreExceptionHandler<TDbContext, TExceptionHandler>()
            where TDbContext : DbContext
            where TExceptionHandler : class, IEfCoreExceptionHandler
        {
            var key = typeof(TDbContext).FullName;

            if (serviceCollection.Any(s =>
                    s.IsKeyedService && ReferenceEquals(s.ServiceKey, key) &&
                    s.ServiceType == typeof(IEfCoreExceptionHandler)))
                return serviceCollection;

            return serviceCollection.AddKeyedTransient<IEfCoreExceptionHandler, TExceptionHandler>(key);
        }

        /// <summary>
        ///     Register the GlobalModelBuilderRegister to the service collection.
        /// </summary>
        /// <typeparam name="TImplementation"></typeparam>
        /// <returns></returns>
        /// <remarks>
        ///     <c>UseAutoConfigModel</c> wraps EF Core's <see cref="IModelCacheKeyFactory" /> so a builder registered
        ///     here after a model was cached makes the next context build a new model. A consumer's
        ///     <c>ReplaceService&lt;IModelCacheKeyFactory, TFactory&gt;()</c> replaces that wrapper, so its cache key
        ///     must also change when a global model builder is registered.
        /// </remarks>
        public IServiceCollection AddGlobalModelBuilder<TImplementation>()
            where TImplementation : class, IGlobalModelBuilder
        {
            // One lock covers the add and the version bump, so no call returns before the version includes the type,
            // even while another thread registers the same type. The bag is filled before the version moves: EF reads
            // the version into the model cache key before it builds the model from the bag, so a build racing a
            // registration can only apply more builders than its key names.
            lock (GlobalModelBuildersLock)
            {
                GlobalModelBuilders.Add(typeof(TImplementation));
                if (DistinctGlobalModelBuilders.Add(typeof(TImplementation)))
                    Interlocked.Increment(ref _globalModelBuildersVersion);
            }

            return serviceCollection;
        }
    }

    internal static readonly ConcurrentBag<Type> GlobalModelBuilders = [];

    private static readonly Lock GlobalModelBuildersLock = new();

    // Read and written only under GlobalModelBuildersLock.
    private static readonly HashSet<Type> DistinctGlobalModelBuilders = [];

    private static int _globalModelBuildersVersion;

    /// <summary>
    ///     Grows by one for each distinct type added to <see cref="GlobalModelBuilders" />. The set is append-only, so
    ///     the version identifies the distinct set; re-registering a type leaves it unchanged.
    /// </summary>
    internal static int GlobalModelBuildersVersion => Volatile.Read(ref _globalModelBuildersVersion);
}