using DKNet.EfCore.Extensions.Extensions;

namespace DKNet.EfCore.Extensions.Internal;

internal sealed class AutoConfigModelCustomizer(ModelCustomizer original) : IModelCustomizer
{
    #region Methods

    private static void ConfigModelCreating(DbContext dbContext, ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var assemblies = GetAssemblies(dbContext);

        //Register Entities
        foreach (var assembly in assemblies)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }

        //Register StaticData Of
        modelBuilder.RegisterDataSeeding(assemblies);

        //Register Global Filter
        modelBuilder.RegisterGlobalModelBuilders(assemblies, dbContext);

        //Register Sequence
        if (dbContext.IsSqlServer())
        {
            modelBuilder.RegisterSequences(assemblies);
        }
    }

    public void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        ConfigModelCreating(context, modelBuilder);
        original.Customize(modelBuilder, context);
    }

    private static Assembly[] GetAssemblies(DbContext dbContext)
    {
        var options = dbContext.GetService<IDbContextOptions>();
        var register = options.FindExtension<EntityAutoConfigRegister>();
        var assemblies = register?.Assemblies ?? [];

        if (assemblies.Length <= 0)
        {
            assemblies = [dbContext.GetType().Assembly];
        }

        return assemblies;
    }

    #endregion
}