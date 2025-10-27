using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKNet.EfCore.Extensions.Configurations;

public abstract class GlobalQueryFilter : IGlobalModelBuilder
{
    private readonly MethodInfo _method = typeof(GlobalQueryFilter)
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

    protected abstract Expression<Func<TEntity, bool>>? HasQueryFilter<TEntity>(DbContext context)
        where TEntity : class;

    private void ApplyQueryFilter<TEntity>(ModelBuilder modelBuilder, DbContext context)
        where TEntity : class
    {
        //TODO: convert to named query filter when migrate to EFCore 10
        var filter = HasQueryFilter<TEntity>(context);
        modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
    }

    protected abstract IEnumerable<IMutableEntityType> GetEntityTypes(ModelBuilder modelBuilder);
}