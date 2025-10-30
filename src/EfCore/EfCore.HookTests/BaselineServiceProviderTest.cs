using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace EfCore.HookTests;

public class BaselineServiceProviderTest(ITestOutputHelper output) : IAsyncLifetime
{
    #region Fields

    private ServiceProvider? _provider;

    #endregion

    #region Methods

    [Fact]
    public async Task Baseline_MultipleApiCalls_ShouldNotCreateTooManyServiceProviders()
    {
        var action = async () =>
        {
            // This test checks if the baseline EF Core setup (without hooks) has the same issue
            // If this also fails, then the issue is not with our hooks

            // Simulate 25 consecutive API calls - this should NOT trigger the EF Core warning
            for (var i = 0; i < 25; i++)
            {
                using var scope = this._provider!.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<HookContext>();

                var entity = new CustomerProfile { Name = $"Baseline Entity {i}" };
                await db.AddAsync(entity);
                await db.SaveChangesAsync();

                output.WriteLine($"Baseline Iteration {i + 1}: Completed");
            }

            output.WriteLine("Baseline test completed without service provider proliferation issues");
        };

        await action.ShouldNotThrowAsync();
    }

    public async Task DisposeAsync()
    {
        if (this._provider != null)
        {
            await this._provider.DisposeAsync();
        }
    }

    public async Task InitializeAsync()
    {
        this._provider = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .AddDbContext<HookContext>(o =>
                o.UseAutoConfigModel([typeof(HookContext).Assembly])
                    .UseSqlite("Data Source=sqlite_baseline_hooks.db"))
            .BuildServiceProvider();

        var db = this._provider.GetRequiredService<HookContext>();
        await db.Database.EnsureCreatedAsync();
    }

    #endregion
}