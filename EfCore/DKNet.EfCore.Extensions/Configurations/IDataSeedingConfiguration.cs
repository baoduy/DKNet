namespace DKNet.EfCore.Extensions.Configurations;

public interface IDataSeedingConfiguration<TEntity> where TEntity : class
{
    ICollection<TEntity> Data { get; }
}