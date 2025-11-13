using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace EfCore.HookTests;

public class PlainEfCoreTest(ITestOutputHelper output) : IAsyncLifetime
{
    #region Fields

    private ServiceProvider _provider = null!;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        _provider = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .AddDbContext<PlainHookContext>(o =>
                o.UseSqlite("Data Source=sqlite_plain_efcore.db"))
            .BuildServiceProvider();

        var db = _provider.GetRequiredService<PlainHookContext>();
        await db.Database.EnsureCreatedAsync();
    }

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

    #endregion
}

// Simple DbContext without any extensions
public class PlainHookContext(DbContextOptions<PlainHookContext> options) : DbContext(options)
{
    #region Properties

    public DbSet<CustomerProfile> CustomerProfiles { get; set; }

    #endregion
}