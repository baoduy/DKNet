using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.Specifications;

/// <summary>
///     The repository specification provider interface.
/// </summary>
public interface IRepositorySpecProvider : IAsyncDisposable, IDisposable
{
    /// <summary>
    ///     The repository specification.
    /// </summary>
    public IRepositorySpec Repository { get; }
}

internal sealed class RepositorySpecProvider<TDbContext>(TDbContext dbContext, IServiceProvider provider)
    : IRepositorySpecProvider
    where TDbContext : DbContext
{
    #region Properties

    public IRepositorySpec Repository { get; } = new RepositorySpec<TDbContext>(dbContext, provider);

    #endregion

    #region Methods

    public void Dispose()
    {
        dbContext.Dispose();
    }

    public ValueTask DisposeAsync() => dbContext.DisposeAsync();

    #endregion
}