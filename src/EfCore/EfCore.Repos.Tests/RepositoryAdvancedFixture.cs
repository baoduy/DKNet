using DKNet.EfCore.Repos;
using DKNet.EfCore.Repos.Abstractions;
using Mapster;
using MapsterMapper;
using Testcontainers.PostgreSql;

namespace EfCore.Repos.Tests;

public class RepositoryAdvancedFixture : IAsyncLifetime
{
    #region Fields

    private readonly PostgreSqlContainer _sqlContainer = new PostgreSqlBuilder()
        .Build();

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
        await _sqlContainer.StopAsync();
        await _sqlContainer.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_sqlContainer.GetConnectionString());

        DbContext = new TestDbContext(optionsBuilder.Options);

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
        RepositoryWithMapper = new Repository<User>(DbContext, mappers);
        RepositoryWithoutMapper = new Repository<User>(DbContext);

        await Task.Delay(TimeSpan.FromSeconds(15));
        await DbContext.Database.EnsureCreatedAsync();
    }

    #endregion
}