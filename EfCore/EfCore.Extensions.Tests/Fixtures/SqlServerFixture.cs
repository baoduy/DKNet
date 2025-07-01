namespace EfCore.Extensions.Tests.Fixtures;

public class SqlServerFixture : IAsyncLifetime
{
    private MsSqlContainer _sql = null!;
    public MyDbContext Db { get; private set; }

    public async Task InitializeAsync()
    {
        _sql = new MsSqlBuilder().WithPassword("a1ckZmGjwV8VqNdBUexV").Build();
        await _sql.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);

        var options = new DbContextOptionsBuilder()
            .LogTo(Console.WriteLine, (eventId, logLevel) => logLevel >= LogLevel.Information
                                                             || eventId == RelationalEventId.CommandExecuting)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .UseSqlServer(_sql.GetConnectionString())
            .UseAutoConfigModel(op => op.ScanFrom(typeof(MyDbContext).Assembly))
            .UseAutoDataSeeding()
            .Options;

        Db = new MyDbContext(options);
        await Db.Database.EnsureCreatedAsync();
    }

    public string GetConnectionString(string dbName) =>
        _sql.GetConnectionString()
            .Replace("Database=master;", $"Database={dbName};", StringComparison.OrdinalIgnoreCase);

    public async Task DisposeAsync()
    {
        await Db.DisposeAsync();
        await _sql.StopAsync();
        await _sql.DisposeAsync();
    }
}