// ReSharper disable CheckNamespace

using System.Diagnostics;
using DKNet.EfCore.Extensions.Configurations;
using DKNet.Fw.Extensions.TypeExtractors;

namespace Microsoft.EntityFrameworkCore;

[SuppressMessage("Major Code Smell",
    "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields")]
public static class EfCoreDataSeedingExtensions
{
    private static readonly MethodInfo AsyncDataSeedingMethodInfo = typeof(EfCoreDataSeedingExtensions)
        .GetMethod(nameof(AsyncDataSeeding), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo DataSeedingMethodInfo = typeof(EfCoreDataSeedingExtensions)
        .GetMethod(nameof(DataSeeding), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static Type[] GetDataSeedingTypes(this ICollection<Assembly> assemblies) =>
    [
        .. assemblies.Extract().Classes().NotGeneric().NotAbstract().NotInterface()
            .IsInstanceOf(typeof(IDataSeedingConfiguration<>))
    ];

    private static Type GetEntityType(this Type instanceType)
    {
        return instanceType.GetInterfaces()
            .First(interfaceType => interfaceType.IsGenericType)
            .GetGenericArguments()[0];
    }

    private static async Task AsyncDataSeeding<TEntity>(DbContext context,
        IDataSeedingConfiguration<TEntity> seedingInstance,
        CancellationToken cancellationToken) where TEntity : class
    {
        Debug.Write($"AsyncDataSeeding for: {typeof(TEntity).Name}");
        var dbSet = context.Set<TEntity>();

        foreach (var entity in seedingInstance.Data)
        {
            //Check whether entity instance exists in db before adding
            var keyValues = context.GetPrimaryKeyValues(entity).ToArray();

            if (await dbSet.FindAsync(keyValues, cancellationToken) == null)
                await dbSet.AddAsync(entity, cancellationToken);
        }
    }

    private static void DataSeeding<TEntity>(DbContext context, IDataSeedingConfiguration<TEntity> seedingInstance)
        where TEntity : class
    {
        Debug.Write($"DataSeeding for: {typeof(TEntity).Name}");
        var dbSet = context.Set<TEntity>();

        foreach (var entity in seedingInstance.Data)
        {
            //Check whether entity instance exists in db before adding
            var keyValues = context.GetPrimaryKeyValues(entity).ToArray();

            if (dbSet.Find(keyValues) == null)
                dbSet.Add(entity);
        }
    }


    private static async Task RunDataSeedingAsync(this DbContext context, Type[] seedingTypes,
        CancellationToken cancellationToken)
    {
        if ((await context.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            return;

        Debug.Write($"Run DataSeedingAsync for: {typeof(DbContext).FullName}");

        foreach (var seedingType in seedingTypes)
        {
            var seedingInstance = Activator.CreateInstance(seedingType);
            if (seedingInstance is null) continue;

            var entityType = seedingType.GetEntityType();

            //Call the seeding method with generic entity type
            var md = AsyncDataSeedingMethodInfo.MakeGenericMethod(entityType);
            await (Task)md.Invoke(null, [context, seedingInstance, cancellationToken])!;

            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static void RunDataSeeding(this DbContext context, Type[] seedingTypes)
    {
        Debug.Write($"Run DataSeeding for: {typeof(DbContext).FullName}");

        foreach (var seedingType in seedingTypes)
        {
            var seedingInstance = Activator.CreateInstance(seedingType);
            if (seedingInstance is null) continue;

            var entityType = seedingType.GetEntityType();

            //Call the seeding method with generic entity type
            var md = DataSeedingMethodInfo.MakeGenericMethod(entityType);
            md.Invoke(null, [context, seedingInstance]);

            context.SaveChanges();
        }
    }

    /// <summary>
    /// </summary>
    public static DbContextOptionsBuilder UseAutoDataSeeding(this DbContextOptionsBuilder @this,
        params ICollection<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(@this);

        if (assemblies.Count == 0)
        {
            var op = @this.GetOrCreateExtension();
            assemblies = [.. op.Registrations.SelectMany(r => r.EntityAssemblies)];
        }

        //Get Alls Seeding Types
        var seedingTypes = GetDataSeedingTypes(assemblies).ToArray();

        @this.UseSeeding((context, performedStoreOperation) =>
        {
            if (!performedStoreOperation)
                return;
            context.RunDataSeeding(seedingTypes);
        });

        @this.UseAsyncSeeding((context, performedStoreOperation, cancellationToken) => !performedStoreOperation
            ? Task.CompletedTask
            : context.RunDataSeedingAsync(seedingTypes, cancellationToken));

        return @this;
    }
}