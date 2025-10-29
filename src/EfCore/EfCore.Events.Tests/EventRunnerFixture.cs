using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;

namespace EfCore.Events.Tests;

public sealed class EventRunnerFixture : IAsyncLifetime
{
    #region Fields

    private SqliteConnection? _connection;

    #endregion

    #region Properties

    public ServiceProvider Provider { get; private set; } = null!;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        if (this._connection != null)
        {
            await this._connection.DisposeAsync();
        }
    }

    public async Task InitializeAsync()
    {
        // Use a shared connection for SQLite in-memory database
        this._connection = new SqliteConnection("DataSource=:memory:");
        await this._connection.OpenAsync();

        TypeAdapterConfig.GlobalSettings.Default.MapToConstructor(true);
        TypeAdapterConfig.GlobalSettings.NewConfig<Root, EntityAddedEvent>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Name, src => src.Name);

        this.Provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(TypeAdapterConfig.GlobalSettings)
            .AddScoped<IMapper, ServiceMapper>()
            .AddDbContextWithHook<DddContext>(o =>
                o.UseSqlite(this._connection).UseAutoConfigModel())
            .AddEventPublisher<DddContext, TestEventPublisher>()
            .BuildServiceProvider();

        //Ensure Db Created
        var db = this.Provider.GetRequiredService<DddContext>();
        await db.Database.EnsureCreatedAsync();
    }

    #endregion
}