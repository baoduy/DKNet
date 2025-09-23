#nullable enable
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace EfCore.DataAuthorization.Tests;

public sealed class DataKeyFixture : IAsyncLifetime
{
    private MsSqlContainer? _sqlContainer;
    public ServiceProvider? Provider { get; private set; }

    public async Task InitializeAsync()
    {
        _sqlContainer = new MsSqlBuilder()
            .WithPassword("a1ckZmGjwV8VqNdBUexV")
            //.WithReuse(true)
            .Build();

        await _sqlContainer!.StartAsync();
        // Wait for SQL Server to be ready
        await Task.Delay(TimeSpan.FromSeconds(20));

        Provider = new ServiceCollection()
            .AddLogging()
            .AddDataOwnerProvider<DddContext, TestDataKeyProvider>()
            .AddDbContextWithHook<DddContext>(builder =>
                builder.UseSqlServer(GetConnectionString())
                    .UseAutoConfigModel())
            .BuildServiceProvider();

        var db = Provider.GetRequiredService<DddContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public string GetConnectionString() =>
        _sqlContainer?.GetConnectionString()
            .Replace("Database=master", "Database=DataKeyDb", StringComparison.OrdinalIgnoreCase) ??
        throw new InvalidOperationException(
            "SQL Server container is not initialized.");
}