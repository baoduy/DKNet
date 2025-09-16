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

        var options = dbContext.GetService<EntityConfigRegisterService>();

        if (options.EntityConfig.Registrations.Count <= 0)
            options.EntityConfig.ScanFrom(dbContext.GetType().Assembly);

        //Register Entities
        modelBuilder.RegisterEntityMappingFrom(options.EntityConfig.Registrations);

        //Register StaticData Of
        //modelBuilder.RegisterStaticDataFrom(options.Registrations);

        //Register Global Filter
        modelBuilder.RegisterGlobalFilterFrom(options.EntityConfig.Registrations, dbContext);

        //Register Sequence
        if (dbContext.IsSqlServer())
            modelBuilder.RegisterSequencesFrom(options.EntityConfig.Registrations);
    }
}