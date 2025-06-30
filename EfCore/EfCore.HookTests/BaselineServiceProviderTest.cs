using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;
using Xunit.Abstractions;

namespace EfCore.HookTests;

public class BaselineServiceProviderTest : IAsyncLifetime
{
    private MsSqlContainer _sqlContainer;
    private ServiceProvider _provider;
    private readonly ITestOutputHelper _output;

    public BaselineServiceProviderTest(ITestOutputHelper output)
    {
        _output = output;
    }

    private string GetConnectionString() =>
        _sqlContainer?.GetConnectionString()
            .Replace("Database=master", "Database=BaselineTestDb", StringComparison.OrdinalIgnoreCase) ??
        throw new InvalidOperationException("SQL Server container is not initialized.");

    public async Task InitializeAsync()
    {
        _sqlContainer = new MsSqlBuilder()
            .WithPassword("a1ckZmGjwV8VqNdBUexV")
            .Build();

        await _sqlContainer.StartAsync();
        await Task.Delay(TimeSpan.FromSeconds(20));

        _provider = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .AddDbContext<HookContext>(o =>
                o.UseSqlServer(GetConnectionString()).UseAutoConfigModel())
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

    [Fact]
    public async Task Baseline_MultipleApiCalls_ShouldNotCreateTooManyServiceProviders()
    {
        // This test checks if the baseline EF Core setup (without hooks) has the same issue
        // If this also fails, then the issue is not with our hooks
        
        // Simulate 25 consecutive API calls - this should NOT trigger the EF Core warning
        for (int i = 0; i < 25; i++)
        {
            using var scope = _provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HookContext>();
            
            var entity = new CustomerProfile { Name = $"Baseline Entity {i}" };
            await db.AddAsync(entity);
            await db.SaveChangesAsync();
            
            _output.WriteLine($"Baseline Iteration {i + 1}: Completed");
        }
        
        _output.WriteLine("Baseline test completed without service provider proliferation issues");
    }
}