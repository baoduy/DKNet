using DKNet.EfCore.Abstractions.Events;
using Microsoft.Data.Sqlite;

namespace EfCore.Events.Tests;

/// <summary>
///     DRK-1904: <c>SaveChangesAsync(acceptAllChangesOnSuccess: false)</c> publishes each domain event once,
///     as the default <c>SaveChangesAsync()</c> already does.
/// </summary>
public class EventHookSaveWithoutAcceptAllChangesTests(EventHookSaveWithoutAcceptAllChangesFixture fixture)
    : IClassFixture<EventHookSaveWithoutAcceptAllChangesFixture>
{
    #region Methods

    [Fact]
    public async Task SaveChangesAsync_WithoutAcceptAllChanges_PublishesDomainEventOnce()
    {
        // Arrange
        await using var scope = fixture.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DddContext>();
        var publisher = scope.ServiceProvider.GetServices<IEventPublisher>()
            .OfType<CountingEventPublisher>().Single();

        var root = new Root("Accept false root", "TestOwner");
        var domainEvent = new EntityAddedEvent { Id = root.Id, Name = root.Name };
        root.AddEvent(domainEvent);
        db.Set<Root>().Add(root);

        // Act
        await db.SaveChangesAsync(false);

        // Assert
        publisher.Published.Count.ShouldBe(1);
        publisher.Published.Single().ShouldBeSameAs(domainEvent);
    }

    #endregion
}

public sealed class EventHookSaveWithoutAcceptAllChangesFixture : IAsyncLifetime
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
        await Provider.DisposeAsync();
        if (_connection != null) await _connection.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        // Use a shared connection for SQLite in-memory database
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        Provider = new ServiceCollection()
            .AddLogging()
            .AddDbContextWithHook<DddContext>(o =>
                o.UseSqlite(_connection).UseAutoConfigModel())
            .AddEventPublisher<DddContext, CountingEventPublisher>()
            .BuildServiceProvider();

        await using var scope = Provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DddContext>().Database.EnsureCreatedAsync();
    }

    #endregion
}

/// <summary>
///     Records every published event on the instance. Scoped per DbContext scope, so no state is shared between tests.
/// </summary>
public sealed class CountingEventPublisher : DefaultEventPublisher
{
    #region Properties

    public List<object> Published { get; } = [];

    #endregion

    #region Methods

    public override Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)
    {
        Published.Add(eventObj);
        return Task.CompletedTask;
    }

    #endregion
}
