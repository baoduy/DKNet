using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;

namespace EfCore.Events.Tests.EventPublisherTests;

public sealed class EvenPublisherFixture : IAsyncLifetime
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

        this.Provider = new ServiceCollection()
            .AddLogging()
            .AddEventPublisher<DddContext, TestEventPublisher>()
            .AddSingleton(TypeAdapterConfig.GlobalSettings)
            .AddScoped<IMapper, ServiceMapper>()
            .AddDbContextWithHook<DddContext>(builder =>
                builder.UseAutoConfigModel(typeof(DddContext).Assembly)
                    .UseSqlite(this._connection))
            .BuildServiceProvider();

        var db = this.Provider.GetRequiredService<DddContext>();
        await db.Database.EnsureCreatedAsync();

        db.Set<Root>()
            .AddRange(
                new Root("Duy", "Steven"),
                new Root("Steven", "Steven"),
                new Root("Hoang", "Steven"),
                new Root("DKNet", "Steven"));
        await db.SaveChangesAsync();
    }

    #endregion
}