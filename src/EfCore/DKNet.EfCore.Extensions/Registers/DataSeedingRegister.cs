using DKNet.EfCore.Extensions.Configurations;

namespace DKNet.EfCore.Extensions.Registers;

internal static class DataSeedingRegister
{
    public static void RegisterDataSeeding(this ModelBuilder modelBuilder,
        AutoEntityRegistrationInfo autoEntityRegistrationInfo)
    {
        var seedingTypes = autoEntityRegistrationInfo.EntityAssemblies.GetDataSeedingTypes();
        foreach (var seedingType in seedingTypes)
        {
            if (Activator.CreateInstance(seedingType) is not IDataSeedingConfiguration seedingInstance) continue;
            var data = seedingInstance.HasData.ToList();
            if (data.Count == 0) continue;

            var entityType = seedingInstance.EntityType;
            modelBuilder.Entity(entityType).HasData(data);
        }
    }
}