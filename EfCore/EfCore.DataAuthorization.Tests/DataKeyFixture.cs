using System;
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Hooks;
using Testcontainers.MsSql;

namespace EfCore.DataAuthorization.Tests;

public sealed class DataKeyFixture : IAsyncLifetime
{
    private MsSqlContainer? _sqlContainer;

    public ServiceProvider Provider { get; private set; }
    public DddContext Context { get; private set; }

    public async Task InitializeAsync()
    {
        _sqlContainer = new MsSqlBuilder()
            .WithPassword("a1ckZmGjwV8VqNdBUexV")
            .WithAutoRemove(true)
            .Build();

        await _sqlContainer!.StartAsync();
        // Wait for SQL Server to be ready
        await Task.Delay(TimeSpan.FromSeconds(20));

        Provider = new ServiceCollection()
            .AddLogging()
            .AddAutoDataKeyProvider<DddContext, TestDataKeyProvider>()
            .AddDbContextWithHook<DddContext>(builder =>
                builder.UseSqlServer(_sqlContainer.GetConnectionString())
                    .UseAutoConfigModel())
            .BuildServiceProvider();

        Context = Provider.GetRequiredService<DddContext>();
        await Context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (Context is not null)
            await Context.DisposeAsync();
        if (Provider is not null)
            await Provider.DisposeAsync();
        if (_sqlContainer is not null)
            await _sqlContainer.DisposeAsync();

    }
}