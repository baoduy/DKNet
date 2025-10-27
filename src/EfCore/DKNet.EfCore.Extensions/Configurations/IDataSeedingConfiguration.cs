namespace DKNet.EfCore.Extensions.Configurations;

public interface IDataSeedingConfiguration
{
    #region Properties

    Type EntityType { get; }
    IEnumerable<dynamic> HasData { get; }
    Func<DbContext, bool, CancellationToken, Task>? SeedAsync { get; }

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

    public Type EntityType => typeof(TEntity);
    protected new virtual ICollection<TEntity> HasData { get; } = [];

    IEnumerable<dynamic> IDataSeedingConfiguration.HasData => HasData;
    public virtual Func<DbContext, bool, CancellationToken, Task>? SeedAsync { get; } = null!;

    #endregion
}