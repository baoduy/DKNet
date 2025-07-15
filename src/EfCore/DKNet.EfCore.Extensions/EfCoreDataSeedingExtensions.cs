// ReSharper disable CheckNamespace

using System.Diagnostics;
using DKNet.EfCore.Extensions.Configurations;
using DKNet.Fw.Extensions.TypeExtractors;

namespace Microsoft.EntityFrameworkCore;

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

    // private static bool IsSqlServer(this DbContext context)
    //     => string.Equals(context.Database.ProviderName, "Microsoft.EntityFrameworkCore.SqlServer",
    //         StringComparison.OrdinalIgnoreCase);


    // [SuppressMessage("Security", "EF1002:Risk of vulnerability to SQL injection.")]
    // private static async Task SetIdentityInsertAsync(this DbContext context, IdentityInserts enable,
    //     string tableName, CancellationToken cancellationToken)
    // {
    //     await context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT [{tableName}] {(enable == IdentityInserts.On ? "ON" : "OFF")}", cancellationToken);
    // }
    //
    // [SuppressMessage("Security", "EF1002:Risk of vulnerability to SQL injection.")]
    // private static void SetIdentityInsert(this DbContext context, IdentityInserts enable,
    //     string tableName)
    // {
    //     context.Database.ExecuteSqlRaw($"SET IDENTITY_INSERT [{tableName}] {(enable == IdentityInserts.On ? "ON" : "OFF")}");
    // }

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
            //var tableName = context.GetTableName(entityType);

            // if (context.IsSqlServer())
            //     await context.SetIdentityInsertAsync(IdentityInserts.On, tableName, cancellationToken);

            //Call the seeding method with generic entity type
            var md = AsyncDataSeedingMethodInfo.MakeGenericMethod(entityType);
            await (Task)md.Invoke(null, [context, seedingInstance, cancellationToken])!;

            await context.SaveChangesAsync(cancellationToken);

            // if (context.IsSqlServer())
            //     await context.SetIdentityInsertAsync(IdentityInserts.Off, tableName, cancellationToken);
        }
    }

    private static void RunDataSeeding(this DbContext context, Type[] seedingTypes)
    {
        if (context.Database.GetPendingMigrations().Any())
            return;

        Debug.Write($"Run DataSeeding for: {typeof(DbContext).FullName}");

        foreach (var seedingType in seedingTypes)
        {
            var seedingInstance = Activator.CreateInstance(seedingType);
            if (seedingInstance is null) continue;

            var entityType = seedingType.GetEntityType();
            //var tableName = context.GetTableName(entityType);

            // if (context.IsSqlServer())
            //     context.SetIdentityInsert(IdentityInserts.On, tableName);

            //Call the seeding method with generic entity type
            var md = DataSeedingMethodInfo.MakeGenericMethod(entityType);
            md.Invoke(null, [context, seedingInstance]);

            context.SaveChanges();

            // if (context.IsSqlServer())
            //     context.SetIdentityInsert(IdentityInserts.Off, tableName);
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

        @this.UseSeeding((context, _) => context.RunDataSeeding(seedingTypes));
        @this.UseAsyncSeeding((context, _, cancellationToken) =>
            context.RunDataSeedingAsync(seedingTypes, cancellationToken));

        return @this;
    }
}