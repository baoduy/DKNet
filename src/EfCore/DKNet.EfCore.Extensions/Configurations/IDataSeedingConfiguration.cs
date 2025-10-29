namespace DKNet.EfCore.Extensions.Configurations;

public interface IDataSeedingConfiguration
{
    #region Properties

    Func<DbContext, bool, CancellationToken, Task>? SeedAsync { get; }

    IEnumerable<dynamic> HasData { get; }

    Type EntityType { get; }

    #endregion
}

/// <summary>
///     This will be using the new `UseSeeding` and `UseAsyncSeeding` that added into Ef9.
///     https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding#model-managed-data
/// </summary>
/// <typeparam name="TEntity"></typeparam>
public abstract class DataSeedingConfiguration<TEntity> : IDataSeedingConfiguration where TEntity : class
{
    #region Properties

    public virtual Func<DbContext, bool, CancellationToken, Task>? SeedAsync { get; } = null!;

    protected virtual ICollection<TEntity> HasData { get; } = [];

    IEnumerable<dynamic> IDataSeedingConfiguration.HasData => this.HasData;

    public Type EntityType => typeof(TEntity);

    #endregion
}