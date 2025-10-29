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
        if (this._connection == null)
        {
            throw new InvalidOperationException("Connection not initialized");
        }

        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(this._connection)
            .UseAutoConfigModel();

        var context = new TestDbContext(optionsBuilder.Options);
        return context;
    }

    public async Task DisposeAsync()
    {
        if (this.DbContext != null)
        {
            await this.DbContext.DisposeAsync();
        }

        if (this._connection != null)
        {
            await this._connection.DisposeAsync();
        }
    }

    public async Task InitializeAsync()
    {
        // Use a shared connection for SQLite in-memory database
        this._connection = new SqliteConnection("DataSource=sqlite_repo_tests.db");
        await this._connection.OpenAsync();

        this.DbContext = this.CreateNewDbContext();
        await this.DbContext.Database.EnsureCreatedAsync();

        this.ReadRepository = new ReadRepository<User>(this.DbContext);
        this.Repository = new Repository<User>(this.DbContext);
    }

    #endregion
}