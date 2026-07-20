using Testcontainers.PostgreSql;

namespace EfCore.Extensions.Tests.Fixtures;

public class PostgresFixture : IAsyncLifetime
{
    #region Fields

    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    #endregion

    #region Properties

    public MyDbContext? Db { get; private set; }

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        if (Db != null) await Db.DisposeAsync();

        await _pg.StopAsync();
        await _pg.DisposeAsync();
    }

    public string GetConnectionString(string dbName) =>
        _pg.GetConnectionString()
            .Replace("Database=postgres;", $"Database={dbName};", StringComparison.OrdinalIgnoreCase);

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();

        var options = new DbContextOptionsBuilder()
            .LogTo(
                Console.WriteLine,
                (eventId, logLevel) => logLevel >= LogLevel.Information
                                       || eventId == RelationalEventId.CommandExecuting)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .UseNpgsql(_pg.GetConnectionString())
            .UseAutoConfigModel([typeof(MyDbContext).Assembly])

            //DONOT use auto seeding here as there are a dedicated test for it
            //.UseAutoDataSeeding()
            .Options;

        Db = new MyDbContext(options);
        await Db.Database.EnsureCreatedAsync();
    }

    #endregion
}