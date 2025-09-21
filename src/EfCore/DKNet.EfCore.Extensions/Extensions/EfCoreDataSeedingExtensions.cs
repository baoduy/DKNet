// ReSharper disable CheckNamespace

using DKNet.EfCore.Extensions.Configurations;
using DKNet.Fw.Extensions.TypeExtractors;

namespace Microsoft.EntityFrameworkCore;

[SuppressMessage("Major Code Smell",
    "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields")]
public static class EfCoreDataSeedingExtensions
{
    internal static Type[] GetDataSeedingTypes(this ICollection<Assembly> assemblies) =>
    [
        .. assemblies.Extract().Classes()
            .IsInstanceOf<IDataSeedingConfiguration>()
    ];


    /// <summary>
    /// </summary>
    public static DbContextOptionsBuilder UseAutoDataSeeding(this DbContextOptionsBuilder @this,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(@this);

        if (assemblies.Length == 0)
        {
            var op = @this.GetOrCreateExtension();
            assemblies = [.. op.Registrations.SelectMany(r => r.EntityAssemblies)];
        }

        //Get Alls Seeding Types
        var seedingTypes = GetDataSeedingTypes(assemblies);
        foreach (var seedingType in seedingTypes)
        {
            if (Activator.CreateInstance(seedingType) is not IDataSeedingConfiguration seedingInstance) continue;
            if (seedingInstance.SeedAsync is null) continue;

            @this.UseSeeding((context, performedStoreOperation) =>
            {
                seedingInstance.SeedAsync.Invoke(context, performedStoreOperation, CancellationToken.None)
                    .GetAwaiter().GetResult();
            });

            @this.UseAsyncSeeding(seedingInstance.SeedAsync);
        }

        return @this;
    }
}