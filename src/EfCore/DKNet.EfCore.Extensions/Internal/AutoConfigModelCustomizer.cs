using DKNet.EfCore.Extensions.Extensions;

namespace DKNet.EfCore.Extensions.Internal;

internal sealed class AutoConfigModelCustomizer(ModelCustomizer original) : IModelCustomizer
{
    #region Methods

    private static void ConfigModelCreating(DbContext dbContext, ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var register = dbContext.GetService<IDbContextOptions>().FindExtension<EntityAutoConfigRegister>();
        var assemblies = GetAssemblies(dbContext, register);

        //Register Entities
        foreach (var assembly in assemblies) modelBuilder.ApplyConfigurationsFromAssembly(assembly);

        //Register StaticData Of
        //modelBuilder.RegisterDataSeeding(assemblies);

        //Register Global Filter
        // Build with the builder set the register was identified by (what is hashed is what is built).
        modelBuilder.RegisterGlobalModelBuilders(
            assemblies,
            register?.GlobalModelBuilders ?? (IEnumerable<Type>)EfCoreSetup.GlobalModelBuilders,
            dbContext);

        //Register Sequence
        if (dbContext.IsSqlServer() || dbContext.IsNpgsql()) modelBuilder.RegisterSequences(assemblies);
    }

    public void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        ConfigModelCreating(context, modelBuilder);
        original.Customize(modelBuilder, context);
    }

    private static Assembly[] GetAssemblies(DbContext dbContext, EntityAutoConfigRegister? register)
    {
        var assemblies = register?.Assemblies ?? [];

        if (assemblies.Length <= 0) assemblies = [dbContext.GetType().Assembly];

        return assemblies;
    }

    #endregion
}