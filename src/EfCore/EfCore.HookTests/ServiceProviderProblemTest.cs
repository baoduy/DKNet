using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;
using Xunit.Abstractions;

namespace EfCore.HookTests;

public class ServiceProviderProblemTest(ITestOutputHelper output) : IAsyncLifetime
{
    private ServiceProvider _provider;
    private MsSqlContainer _sqlContainer;

    public async Task InitializeAsync()
    {
        _sqlContainer = new MsSqlBuilder()
            .WithPassword("a1ckZmGjwV8VqNdBUexV")
            .Build();

        await _sqlContainer.StartAsync();
        await Task.Delay(TimeSpan.FromSeconds(20));

        _provider = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .AddDbContextWithHook<HookContext>(o =>
                o.UseSqlServer(GetConnectionString()).UseAutoConfigModel())
            .AddHook<HookContext, Hook>()
            .BuildServiceProvider();

        var db = _provider.GetRequiredService<HookContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (_sqlContainer != null)
            await _sqlContainer.DisposeAsync();
    }

    private string GetConnectionString() =>
        _sqlContainer?.GetConnectionString()
            .Replace("Database=master", "Database=ServiceProviderTestDb", StringComparison.OrdinalIgnoreCase) ??
        throw new InvalidOperationException("SQL Server container is not initialized.");

    [Fact]
    public async Task MultipleApiCalls_ShouldNotCreateTooManyServiceProviders()
    {
        var action = async () =>
        {
            // This test simulates multiple API calls that each create entities
            // This should reproduce the "More than twenty IServiceProvider instances" issue

            var hook = _provider.GetRequiredKeyedService<Hook>(typeof(HookContext).FullName);

            // Simulate 25 consecutive API calls - this should trigger the EF Core warning
            for (var i = 0; i < 25; i++)
            {
                hook.Reset();

                using var scope = _provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<HookContext>();

                var entity = new CustomerProfile { Name = $"Test Entity {i}" };
                await db.AddAsync(entity);
                await db.SaveChangesAsync();

                output.WriteLine($"Iteration {i + 1}: Hook Before={hook.BeforeCalled}, After={hook.AfterCalled}");
            }

            // The test itself may pass but we should see the EF Core warning in logs
            output.WriteLine(
                "If you see 'More than twenty IServiceProvider instances' warning above, the issue is reproduced");
        };
        await action.ShouldNotThrowAsync();
    }
}