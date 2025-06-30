#nullable enable
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Hooks;
using Testcontainers.MsSql;

namespace EfCore.DataAuthorization.Tests;

public sealed class DataKeyAdvancedFixture : IAsyncLifetime
{
    private MsSqlContainer? _sqlContainer;
    public ServiceProvider Provider { get; private set; } = null!;

    public string GetConnectionString() =>
        _sqlContainer?.GetConnectionString()
            .Replace("Database=master", "Database=DataKeyAdvancedDb", StringComparison.OrdinalIgnoreCase) ??
        throw new InvalidOperationException(
            "SQL Server container is not initialized.");

    public async Task InitializeAsync()
    {
        _sqlContainer = new MsSqlBuilder()
            .WithPassword("a1ckZmGjwV8VqNdBUexV")
            .Build();

        await _sqlContainer!.StartAsync();
        // Wait for SQL Server to be ready
        await Task.Delay(TimeSpan.FromSeconds(20));

        Provider = new ServiceCollection()
            .AddLogging()
            .AddAutoDataKeyProvider<DddContext, TestDataKeyProvider>()
            .AddDbContextWithHook<DddContext>(builder =>
                builder.UseSqlServer(GetConnectionString())
                    .UseAutoConfigModel())
            .BuildServiceProvider();

        var db = Provider.GetRequiredService<DddContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_sqlContainer != null)
        {
            await _sqlContainer.DisposeAsync();
        }
        Provider?.Dispose();
    }
}