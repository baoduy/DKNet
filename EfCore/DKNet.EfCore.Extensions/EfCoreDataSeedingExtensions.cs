// ReSharper disable CheckNamespace

using System.Reflection;
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
            .IsInstanceOf(typeof(IDataSeedingConfiguration<>)),
    ];

    private static Type GetEntityType(this Type instanceType)
        => instanceType.GetInterfaces()
            .First(interfaceType => interfaceType.IsGenericType)
            .GetGenericArguments()[0];

    private static async Task AsyncDataSeeding<TEntity>(DbContext context,
        IDataSeedingConfiguration<TEntity> seedingInstance,
        CancellationToken cancellationToken) where TEntity : class
    {
        var dbSet = context.Set<TEntity>();

        foreach (var entity in seedingInstance.Data)
        {
            //Check whether entity instance exists in db before adding
            var keyValues = context.GetPrimaryKeyValues(entity).ToArray();

            if (await dbSet.FindAsync(keyValues, cancellationToken: cancellationToken) == null)
                dbSet.Add(entity);
        }
    }

    private static void DataSeeding<TEntity>(DbContext context, IDataSeedingConfiguration<TEntity> seedingInstance)
        where TEntity : class
    {
        var dbSet = context.Set<TEntity>();

        foreach (var entity in seedingInstance.Data)
        {
            //Check whether entity instance exists in db before adding
            var keyValues = context.GetPrimaryKeyValues(entity).ToArray();

            if (dbSet.Find(keyValues) == null)
                dbSet.Add(entity);
        }
    }

    private static bool IsSqlServer(this DbContext context)
        => string.Equals(context.Database.ProviderName, "Microsoft.EntityFrameworkCore.SqlServer",
            StringComparison.OrdinalIgnoreCase);


    private enum IdentityInserts
    {
        On,
        Off,
    }

    private static async Task SetIdentityInsertAsync(this DbContext context, IdentityInserts enable,
        ICollection<string> tableNames, CancellationToken cancellationToken)
    {
        var sql = string.Join(';',
            tableNames.Select(t => $"SET IDENTITY_INSERT [{t}] {(enable == IdentityInserts.On ? "ON" : "OFF")}"));
        await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static void SetIdentityInsert(this DbContext context, IdentityInserts enable,
        ICollection<string> tableNames)
    {
        var sql = string.Join(';',
            tableNames.Select(t => $"SET IDENTITY_INSERT [{t}] {(enable == IdentityInserts.On ? "ON" : "OFF")}"));
        context.Database.ExecuteSqlRaw(sql);
    }

    private static async Task RunDataSeedingAsync(this DbContext context, Type[] seedingTypes,
        CancellationToken cancellationToken)
    {
        var tableNames = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var seedingType in seedingTypes)
        {
            var seedingInstance = Activator.CreateInstance(seedingType);
            if (seedingInstance is null) continue;

            var entityType = seedingType.GetEntityType();
            tableNames.Add(context.GetTableName(entityType));

            //Call the seeding method with generic entity type
            var md = AsyncDataSeedingMethodInfo.MakeGenericMethod(entityType);
            await (Task)md.Invoke(null, [context, seedingInstance, cancellationToken])!;
        }

        if (context.IsSqlServer())
            await context.SetIdentityInsertAsync(IdentityInserts.On, tableNames, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        if (context.IsSqlServer())
            await context.SetIdentityInsertAsync(IdentityInserts.Off, tableNames, cancellationToken);
    }

    private static void RunDataSeeding(this DbContext context, Type[] seedingTypes)
    {
        var tableNames = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var seedingType in seedingTypes)
        {
            var seedingInstance = Activator.CreateInstance(seedingType);
            if (seedingInstance is null) continue;

            var entityType = seedingType.GetEntityType();
            tableNames.Add(context.GetTableName(entityType));

            //Call the seeding method with generic entity type
            var md = DataSeedingMethodInfo.MakeGenericMethod(entityType);
            md.Invoke(null, [context, seedingInstance]);
        }

        if (context.IsSqlServer())
            context.SetIdentityInsert(IdentityInserts.On, tableNames);

        context.SaveChanges();

        if (context.IsSqlServer())
            context.SetIdentityInsert(IdentityInserts.Off, tableNames);
    }
    /// <summary>
    ///
    /// </summary>
    public static DbContextOptionsBuilder UseAutoDataSeeding(this DbContextOptionsBuilder @this,
        params ICollection<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(@this);

        if (assemblies.Count == 0)
        {
            var op = @this.GetOrCreateExtension();
            assemblies = op.Registrations.SelectMany(r => r.EntityAssemblies).ToList();
        }

        //Get Alls Seeding Types
        var seedingTypes = GetDataSeedingTypes(assemblies).ToArray();

        @this.UseAsyncSeeding( (context, _, cancellationToken) => context.RunDataSeedingAsync(seedingTypes, cancellationToken));

        @this.UseSeeding( (context, _) => context.RunDataSeeding(seedingTypes));

        return @this;
    }
}