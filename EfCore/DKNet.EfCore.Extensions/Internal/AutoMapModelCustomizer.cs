using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using DKNet.EfCore.Extensions.Registers;

namespace DKNet.EfCore.Extensions.Internal;

internal sealed class AutoMapModelCustomizer(ModelCustomizer original) : IModelCustomizer
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

        var options = dbContext.GetService<EntityMappingRegisterService>()?.EntityMapping;
        if (options == null) return;

        if (options.Registrations.Count <= 0)
            options.ScanFrom(dbContext.GetType().Assembly);

        //Register Entities
        modelBuilder.RegisterEntityMappingFrom(options.Registrations);

        //Register StaticData Of
        modelBuilder.RegisterStaticDataFrom(options.Registrations);

        //Register Global Filter
        modelBuilder.RegisterGlobalFilterFrom(options.Registrations, dbContext);

        //Register Sequence
        if (dbContext.Database.IsSequenceSupported())
            modelBuilder.RegisterSequencesFrom(options.Registrations);
    }
}