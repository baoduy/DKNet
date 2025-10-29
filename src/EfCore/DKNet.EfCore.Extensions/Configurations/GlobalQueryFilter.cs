using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DKNet.EfCore.Extensions.Configurations;

public abstract class GlobalQueryFilter : IGlobalModelBuilder
{
    #region Fields

    private readonly MethodInfo _method = typeof(GlobalQueryFilter)
        .GetMethod(nameof(ApplyQueryFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

    #endregion

    #region Methods

    public void Apply(ModelBuilder modelBuilder, DbContext context)
    {
        var entityTypes = this.GetEntityTypes(modelBuilder);

        foreach (var entityType in entityTypes)
        {
            var genericMethod = this._method.MakeGenericMethod(entityType.ClrType);
            genericMethod.Invoke(this, [modelBuilder, context]);
        }
    }

    private void ApplyQueryFilter<TEntity>(ModelBuilder modelBuilder, DbContext context)
        where TEntity : class
    {
        //TODO: convert to named query filter when migrate to EFCore 10
        var filter = this.HasQueryFilter<TEntity>(context);
        modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
    }

    protected abstract IEnumerable<IMutableEntityType> GetEntityTypes(ModelBuilder modelBuilder);

    protected abstract Expression<Func<TEntity, bool>>? HasQueryFilter<TEntity>(DbContext context)
        where TEntity : class;

    #endregion
}