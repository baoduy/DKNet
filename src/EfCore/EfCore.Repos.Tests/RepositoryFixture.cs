using DKNet.EfCore.Repos;
using DKNet.EfCore.Repos.Abstractions;
using Microsoft.Data.Sqlite;

namespace EfCore.Repos.Tests;

public class RepositoryFixture : IAsyncLifetime
{
    #region Fields

    private SqliteConnection? _connection;

    #endregion

    #region Properties

    public DbContext? DbContext { get; set; }

    public IReadRepository<User> ReadRepository { get; set; } = null!;

    public IRepository<User> Repository { get; set; } = null!;

    #endregion

    #region Methods

    public DbContext CreateNewDbContext()
    {
        if (_connection == null) throw new InvalidOperationException("Connection not initialized");

        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .UseAutoConfigModel();

        var context = new TestDbContext(optionsBuilder.Options);
        return context;
    }

    public async Task DisposeAsync()
    {
        if (DbContext != null) await DbContext.DisposeAsync();

        if (_connection != null) await _connection.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        // Use in-memory SQLite database that is deleted on connection close
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        DbContext = CreateNewDbContext();
        await DbContext.Database.EnsureCreatedAsync();

        ReadRepository = new ReadRepository<User>(DbContext);
        Repository = new Repository<User>(DbContext);
    }

    #endregion
}