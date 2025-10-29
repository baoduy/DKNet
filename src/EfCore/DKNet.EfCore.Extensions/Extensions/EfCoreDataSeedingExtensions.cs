// ReSharper disable CheckNamespace

using DKNet.EfCore.Extensions.Configurations;
using DKNet.Fw.Extensions.TypeExtractors;

namespace Microsoft.EntityFrameworkCore;

[SuppressMessage(
    "Major Code Smell",
    "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields")]
public static class EfCoreDataSeedingExtensions
{
    #region Methods

    internal static Type[] GetDataSeedingTypes(this ICollection<Assembly> assemblies) =>
    [
        .. assemblies.Extract().Classes()
            .IsInstanceOf<IDataSeedingConfiguration>()
    ];

    internal static void RegisterDataSeeding(this ModelBuilder modelBuilder, params Assembly[] assemblies)
    {
        var seedingTypes = assemblies.GetDataSeedingTypes();
        foreach (var seedingType in seedingTypes)
        {
            if (Activator.CreateInstance(seedingType) is not IDataSeedingConfiguration seedingInstance)
            {
                continue;
            }

            var data = seedingInstance.HasData.ToList();
            if (data.Count == 0)
            {
                continue;
            }

            var entityType = seedingInstance.EntityType;
            modelBuilder.Entity(entityType).HasData(data);
        }
    }

    /// <summary>
    /// </summary>
    public static DbContextOptionsBuilder UseAutoDataSeeding(this DbContextOptionsBuilder @this, Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(@this);

        //Get Alls Seeding Types
        var seedingTypes = GetDataSeedingTypes(assemblies);
        var seedingInstances = seedingTypes.Select(s => Activator.CreateInstance(s) as IDataSeedingConfiguration);

        @this.UseSeeding((context, performedStoreOperation) =>
        {
            foreach (var s in seedingInstances.OfType<IDataSeedingConfiguration>())
            {
                s.SeedAsync?.Invoke(context, performedStoreOperation, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
        });

        @this.UseAsyncSeeding(async (context, performedStoreOperation, cancellation) =>
        {
            foreach (var s in seedingInstances.OfType<IDataSeedingConfiguration>())
            {
                if (s.SeedAsync != null)
                {
                    await s.SeedAsync.Invoke(context, performedStoreOperation, cancellation);
                }
            }
        });

        return @this;
    }

    #endregion
}