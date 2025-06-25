using Mapster;
using MapsterMapper;

namespace EfCore.Events.Tests.EventPublisherTests;

public sealed class EvenPublisherFixture : IDisposable
{
    private readonly MsSqlContainer _sqlContainer;

    public EvenPublisherFixture()
    {
        _sqlContainer = SqlServerTestHelper.StartSqlContainerAsync().GetAwaiter().GetResult();
        
        Provider = new ServiceCollection()
            .AddLogging()
            .AddEventPublisher<DddContext, TestEventPublisher>()
            .AddSingleton(TypeAdapterConfig.GlobalSettings)
            .AddScoped<IMapper, ServiceMapper>()
            .AddDbContextWithHook<DddContext>(builder => builder.UseSqlServer(_sqlContainer.GetConnectionString()).UseAutoConfigModel())
            .BuildServiceProvider();

        Context = Provider.GetRequiredService<DddContext>();
        Context.Database.EnsureCreated();


        Context.Set<Root>()
            .AddRange(new Root("Duy"), new Root("Steven"), new Root("Hoang"), new Root("DKNet"));

        //BeforeAddedEventTestHandler.ReturnFailureResult = false;
        Context.SaveChangesAsync().GetAwaiter().GetResult();
    }

    public ServiceProvider Provider { get; }
    public DddContext Context { get; }

    public void Dispose()
    {
        Provider?.Dispose();
        Context?.Dispose();
        SqlServerTestHelper.CleanupContainerAsync(_sqlContainer).GetAwaiter().GetResult();
    }
}