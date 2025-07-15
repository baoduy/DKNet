namespace DKNet.EfCore.Extensions.Configurations;

/// <summary>
///     This will be using the new `UseSeeding` and `UseAsyncSeeding` that added into Ef9.
///     https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding#model-managed-data
/// </summary>
/// <typeparam name="TEntity"></typeparam>
public interface IDataSeedingConfiguration<TEntity> where TEntity : class
{
    ICollection<TEntity> Data { get; }
}