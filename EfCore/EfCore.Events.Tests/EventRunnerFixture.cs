namespace EfCore.Events.Tests;

public sealed class EventRunnerFixture : IDisposable
{
    public EventRunnerFixture()
    {
        Provider = new ServiceCollection()
            .AddLogging()
            .AddCoreInfraServices<DddContext>(builder => builder.UseSqliteMemory())
            .BuildServiceProvider();

        Context = Provider.GetRequiredService<DddContext>();
        Context.Database.EnsureCreated();

        //Add Root
        Context.Add(new Root("Steven"));
        Context.SaveChangesAsync().GetAwaiter().GetResult();
    }

    public ServiceProvider Provider { get; }
    public DddContext Context { get; }

    public void Dispose()
    {
        Provider?.Dispose();
        Context?.Dispose();
    }
}