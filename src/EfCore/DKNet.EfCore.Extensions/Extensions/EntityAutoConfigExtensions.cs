using DKNet.EfCore.Extensions.Configurations;
using DKNet.Fw.Extensions.TypeExtractors;

// ReSharper disable CheckNamespace
namespace Microsoft.EntityFrameworkCore;

[SuppressMessage("Major Code Smell",
    "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields")]
internal static class EntityAutoConfigExtensions
{
    /// <summary>
    ///     Scan GlobalFilter from Assemblies
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>An enumerable of global filter types.</returns>
    private static IEnumerable<Type> GetGlobalFilters(this Assembly assemblies) =>
        assemblies.Extract().Classes().NotAbstract().IsInstanceOf<IGlobalQueryFilter>();

    /// <summary>
    ///     Register GlobalFilter from RegistrationInfo <see />
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="assembly"></param>
    /// <param name="dbContext">The database context.</param>
    internal static void RegisterGlobalFilters(this ModelBuilder modelBuilder, Assembly assembly, DbContext dbContext)
    {
        var globalFilters = assembly.GetGlobalFilters()
            .Union(EfCoreSetup.GlobalQueryFilters)
            .Distinct();

        foreach (var filter in globalFilters)
        {
            var filterInstance = Activator.CreateInstance(filter) as IGlobalQueryFilter;
            filterInstance?.Apply(modelBuilder, dbContext);
        }
    }
}