using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.Specifications;

/// <summary>
///     The repository specification factory interface.
/// </summary>
public interface IRepositorySpecFactory
{
    /// <summary>
    ///     Create a new repository specification.
    /// </summary>
    /// <returns></returns>
    IRepositorySpecProvider CreateAsync<TDbContext>() where TDbContext : DbContext;
}

/// <inheritdoc />
internal sealed class RepositorySpecFactory(IServiceProvider provider) : IRepositorySpecFactory
{
    #region Methods

    /// <inheritdoc />
    public IRepositorySpecProvider CreateAsync<TDbContext>() where TDbContext : DbContext
    {
        var dbFactory = provider.GetRequiredService<IDbContextFactory<TDbContext>>();
        var dbContext = dbFactory.CreateDbContext();
        return new RepositorySpecProvider<TDbContext>(dbContext, provider);
    }

    #endregion
}