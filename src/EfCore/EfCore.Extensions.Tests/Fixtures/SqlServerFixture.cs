namespace EfCore.Extensions.Tests.Fixtures;

public class SqlServerFixture : IAsyncLifetime
{
    #region Fields

    private readonly MsSqlContainer _sql = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    #endregion

    #region Properties

    public MyDbContext? Db { get; private set; }

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        if (Db != null) await Db.DisposeAsync();

        await _sql.StopAsync();
        await _sql.DisposeAsync();
    }

    public string GetConnectionString(string dbName) =>
        _sql.GetConnectionString()
            .Replace("Database=master;", $"Database={dbName};", StringComparison.OrdinalIgnoreCase);

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        await Task.Delay(TimeSpan.FromSeconds(5));

        var options = new DbContextOptionsBuilder()
            .LogTo(
                Console.WriteLine,
                (eventId, logLevel) => logLevel >= LogLevel.Information
                                       || eventId == RelationalEventId.CommandExecuting)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .UseSqlServer(_sql.GetConnectionString())
            .UseAutoConfigModel([typeof(MyDbContext).Assembly])

            //DONOT use auto seeding here as there are a dedicated test for it
            //.UseAutoDataSeeding()
            .Options;

        Db = new MyDbContext(options);
        await Db.Database.EnsureCreatedAsync();
    }

    #endregion
}