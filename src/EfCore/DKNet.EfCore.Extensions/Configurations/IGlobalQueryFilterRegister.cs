using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKNet.EfCore.Extensions.Configurations;

/// <summary>
///     This will be scans from the Assemblies when registering the services.
///     Use this to apply the global filter for entity or add custom entities into DbContext.
/// </summary>
public interface IGlobalQueryFilterRegister
{
    void Apply(ModelBuilder modelBuilder, DbContext context);
}

public abstract class GlobalQueryFilterRegister : IGlobalQueryFilterRegister
{
    private readonly MethodInfo _method = typeof(GlobalQueryFilterRegister)
        .GetMethod(nameof(ApplyQueryFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

    public void Apply(ModelBuilder modelBuilder, DbContext context)
    {
        var entityTypes = GetEntityTypes(modelBuilder);

        foreach (var entityType in entityTypes)
        {
            var genericMethod = _method.MakeGenericMethod(entityType.ClrType);
            genericMethod.Invoke(this, [modelBuilder, context]);
        }
    }

    protected abstract void HasQueryFilter<TEntity>(EntityTypeBuilder<TEntity> builder, DbContext context)
        where TEntity : class;

    private void ApplyQueryFilter<TEntity>(ModelBuilder modelBuilder, DbContext context)
        where TEntity : class
    {
        //TODO: convert to named query filter when migrate to EFCore 10
        HasQueryFilter(modelBuilder.Entity<TEntity>(), context);
    }

    protected abstract IEnumerable<IMutableEntityType> GetEntityTypes(ModelBuilder modelBuilder);
}