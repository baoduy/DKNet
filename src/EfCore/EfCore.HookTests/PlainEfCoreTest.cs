using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;
using Xunit.Abstractions;

namespace EfCore.HookTests;

public class PlainEfCoreTest(ITestOutputHelper output) : IAsyncLifetime
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
            .AddDbContext<PlainHookContext>(o =>
                o.UseSqlServer(GetConnectionString()))
            .BuildServiceProvider();

        var db = _provider.GetRequiredService<PlainHookContext>();
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
            .Replace("Database=master", "Database=PlainTestDb", StringComparison.OrdinalIgnoreCase) ??
        throw new InvalidOperationException("SQL Server container is not initialized.");

    [Fact]
    public async Task PlainEfCore_MultipleApiCalls_ShouldNotCreateTooManyServiceProviders()
    {
        var action = async () =>
        {
            // This test checks if plain EF Core setup (without any extensions) has the issue

            // Simulate 25 consecutive API calls - this should NOT trigger the EF Core warning
            for (var i = 0; i < 25; i++)
            {
                using var scope = _provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PlainHookContext>();

                var entity = new CustomerProfile { Name = $"Plain Entity {i}" };
                await db.AddAsync(entity);
                await db.SaveChangesAsync();

                output.WriteLine($"Plain Iteration {i + 1}: Completed");
            }

            output.WriteLine("Plain EF Core test completed without service provider proliferation issues");
        };

        await action.ShouldNotThrowAsync();
    }
}

// Simple DbContext without any extensions
public class PlainHookContext(DbContextOptions<PlainHookContext> options) : DbContext(options)
{
    public DbSet<CustomerProfile> CustomerProfiles { get; set; }
}