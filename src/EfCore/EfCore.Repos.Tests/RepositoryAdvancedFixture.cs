using DKNet.EfCore.Repos;
using DKNet.EfCore.Repos.Abstractions;
using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace EfCore.Repos.Tests;

public class RepositoryAdvancedFixture : IAsyncLifetime
{
    #region Fields

    private SqliteConnection? _connection;

    #endregion

    #region Properties

    public TestDbContext DbContext { get; set; } = null!;

    public IReadRepository<User> ReadRepositoryWithMapper { get; set; } = null!;

    public IReadRepository<User> ReadRepositoryWithoutMapper { get; set; } = null!;

    public IRepository<User> RepositoryWithMapper { get; set; } = null!;

    public IRepository<User> RepositoryWithoutMapper { get; set; } = null!;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        await DbContext.DisposeAsync();
        if (_connection != null) await _connection.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        // Use in-memory SQLite database that is deleted on connection close
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection);

        DbContext = new TestDbContext(optionsBuilder.Options);
        await DbContext.Database.EnsureCreatedAsync();

        // Configure Mapster
        var config = new TypeAdapterConfig();
        config.NewConfig<User, UserDto>()
            .Map(dest => dest.FirstName, src => src.FirstName)
            .Map(dest => dest.LastName, src => src.LastName);

        var mapper = new Mapper(config);
        var mappers = new[] { mapper };

        // Create repositories with and without mappers
        ReadRepositoryWithMapper = new ReadRepository<User>(DbContext, mappers);
        ReadRepositoryWithoutMapper = new ReadRepository<User>(DbContext);
        RepositoryWithMapper = new Repository<User>(DbContext, mapper);
        RepositoryWithoutMapper = new Repository<User>(DbContext, new ServiceCollection().BuildServiceProvider());
    }

    #endregion
}