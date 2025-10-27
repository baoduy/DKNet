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
    private static IEnumerable<Type> GetGlobalFilters(this Assembly[] assemblies) =>
        assemblies.Extract().Classes().NotAbstract().IsInstanceOf<IGlobalModelBuilder>();

    /// <summary>
    ///     Register GlobalFilter from RegistrationInfo <see />
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="assemblies"></param>
    /// <param name="dbContext">The database context.</param>
    internal static void RegisterGlobalModelBuilders(this ModelBuilder modelBuilder, Assembly[] assemblies,
        DbContext dbContext)
    {
        var globalFilters = assemblies.GetGlobalFilters()
            .Union(EfCoreSetup.GlobalQueryFilters)
            .Distinct();

        foreach (var filter in globalFilters)
        {
            var filterInstance = Activator.CreateInstance(filter) as IGlobalModelBuilder;
            filterInstance?.Apply(modelBuilder, dbContext);
        }
    }
}