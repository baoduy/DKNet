using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

internal sealed class RepositorySpecProvider<TDbContext>
    : IRepositorySpecProvider
    where TDbContext : DbContext
{
    #region Fields

    private readonly TDbContext _dbContext;
    private readonly IServiceScope _provider;

    #endregion

    #region Constructors

    public RepositorySpecProvider(IServiceProvider provider)
    {
        _provider = provider.CreateScope();
        _dbContext = _provider.ServiceProvider.GetRequiredService<IDbContextFactory<TDbContext>>().CreateDbContext();
        Repository = new RepositorySpec<TDbContext>(_dbContext, _provider.ServiceProvider);
    }

    #endregion

    #region Properties

    public IRepositorySpec Repository { get; }

    #endregion

    #region Methods

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync().ConfigureAwait(false);
        if (_provider is IAsyncDisposable p) await p.DisposeAsync().ConfigureAwait(false);
        else _provider.Dispose();
    }

    #endregion
}