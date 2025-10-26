using DKNet.EfCore.Extensions.Extensions;

namespace DKNet.EfCore.Extensions.Internal;

internal sealed class AutoConfigModelCustomizer(ModelCustomizer original) : IModelCustomizer
{
    public void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        ConfigModelCreating(context, modelBuilder);
        original.Customize(modelBuilder, context);
    }

    private static void ConfigModelCreating(DbContext dbContext, ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var assemblies = dbContext.GetType().Assembly;

        //Register Entities
        modelBuilder.ApplyConfigurationsFromAssembly(assemblies);

        //Register StaticData Of
        modelBuilder.RegisterDataSeedingFrom(assemblies);

        //Register Global Filter
        modelBuilder.RegisterGlobalFilters(assemblies, dbContext);

        //Register Sequence
        if (dbContext.IsSqlServer())
            modelBuilder.RegisterSequencesFrom(assemblies);
    }
}