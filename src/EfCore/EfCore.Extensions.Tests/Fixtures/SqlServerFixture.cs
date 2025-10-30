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
        if (this.Db != null)
        {
            await this.Db.DisposeAsync();
        }

        await this._sql.StopAsync();
        await this._sql.DisposeAsync();
    }

    public string GetConnectionString(string dbName) =>
        this._sql.GetConnectionString()
            .Replace("Database=master;", $"Database={dbName};", StringComparison.OrdinalIgnoreCase);

    public async Task InitializeAsync()
    {
        await this._sql.StartAsync();
        await Task.Delay(TimeSpan.FromSeconds(5));

        var options = new DbContextOptionsBuilder()
            .LogTo(
                Console.WriteLine,
                (eventId, logLevel) => logLevel >= LogLevel.Information
                                       || eventId == RelationalEventId.CommandExecuting)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .UseSqlServer(this._sql.GetConnectionString())
            .UseAutoConfigModel([typeof(MyDbContext).Assembly])

            //DONOT use auto seeding here as there are a dedicated test for it
            //.UseAutoDataSeeding()
            .Options;

        this.Db = new MyDbContext(options);
        await this.Db.Database.EnsureCreatedAsync();
    }

    #endregion
}