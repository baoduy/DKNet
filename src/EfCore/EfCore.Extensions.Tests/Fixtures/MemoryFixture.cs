namespace EfCore.Extensions.Tests.Fixtures;

public class MemoryFixture : IAsyncLifetime
{
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
    }

    public async Task InitializeAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);

        var options = new DbContextOptionsBuilder()
            .LogTo(
                Console.WriteLine,
                (eventId, logLevel) => logLevel >= LogLevel.Information
                                       || eventId == RelationalEventId.CommandExecuting)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .UseInMemoryDatabase("testDb")
            .UseAutoConfigModel([typeof(MyDbContext).Assembly])

            //DONOT use auto seeding here as there are a dedicated test for it
            //.UseAutoDataSeeding()
            .Options;

        this.Db = new MyDbContext(options);
        await this.Db.Database.EnsureCreatedAsync();
    }

    #endregion
}