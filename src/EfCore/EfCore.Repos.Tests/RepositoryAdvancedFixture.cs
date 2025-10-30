using DKNet.EfCore.Repos;
using DKNet.EfCore.Repos.Abstractions;
using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;

namespace EfCore.Repos.Tests;

public class RepositoryAdvancedFixture : IAsyncLifetime
{
    #region Fields

    private SqliteConnection? _connection;

    #endregion

    #region Properties

    public IReadRepository<User> ReadRepositoryWithMapper { get; set; } = null!;

    public IReadRepository<User> ReadRepositoryWithoutMapper { get; set; } = null!;

    public IRepository<User> RepositoryWithMapper { get; set; } = null!;

    public IRepository<User> RepositoryWithoutMapper { get; set; } = null!;

    public TestDbContext DbContext { get; set; } = null!;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        await this.DbContext.DisposeAsync();
        if (this._connection != null)
        {
            await this._connection.DisposeAsync();
        }
    }

    public async Task InitializeAsync()
    {
        // Use in-memory SQLite database that is deleted on connection close
        this._connection = new SqliteConnection("DataSource=:memory:");
        await this._connection.OpenAsync();

        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(this._connection);

        this.DbContext = new TestDbContext(optionsBuilder.Options);
        await this.DbContext.Database.EnsureCreatedAsync();

        // Configure Mapster
        var config = new TypeAdapterConfig();
        config.NewConfig<User, UserDto>()
            .Map(dest => dest.FirstName, src => src.FirstName)
            .Map(dest => dest.LastName, src => src.LastName);

        var mapper = new Mapper(config);
        var mappers = new[] { mapper };

        // Create repositories with and without mappers
        this.ReadRepositoryWithMapper = new ReadRepository<User>(this.DbContext, mappers);
        this.ReadRepositoryWithoutMapper = new ReadRepository<User>(this.DbContext);
        this.RepositoryWithMapper = new Repository<User>(this.DbContext, mappers);
        this.RepositoryWithoutMapper = new Repository<User>(this.DbContext);
    }

    #endregion
}