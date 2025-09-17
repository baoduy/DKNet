namespace DKNet.EfCore.Extensions.Configurations;

public interface IDataSeedingConfiguration
{
    Type EntityType { get; }
    IEnumerable<dynamic> HasData { get; }
    Func<DbContext, bool, CancellationToken, Task>? SeedAsync { get; }
}

/// <summary>
///     This will be using the new `UseSeeding` and `UseAsyncSeeding` that added into Ef9.
///     https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding#model-managed-data
/// </summary>
/// <typeparam name="TEntity"></typeparam>
public abstract class DataSeedingConfiguration<TEntity> : IDataSeedingConfiguration where TEntity : class
{
    public Type EntityType => typeof(TEntity);
    protected new virtual ICollection<TEntity> HasData { get; } = [];
    public virtual Func<DbContext, bool, CancellationToken, Task>? SeedAsync { get; } = null!;

    IEnumerable<dynamic> IDataSeedingConfiguration.HasData => HasData;
}