using System;
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Hooks;

namespace EfCore.DataAuthorization.Tests;

public sealed class DataKeyFixture : IDisposable
{
    private readonly MsSqlContainer _sqlContainer;

    public DataKeyFixture()
    {
        _sqlContainer = SqlServerTestHelper.StartSqlContainerAsync().GetAwaiter().GetResult();
        
        Provider = new ServiceCollection()
            .AddLogging()
            .AddAutoDataKeyProvider<DddContext, TestDataKeyProvider>()
            .AddDbContextWithHook<DddContext>(builder =>
                builder.UseSqlServer(_sqlContainer.GetConnectionString())
                    .UseAutoConfigModel())
            .BuildServiceProvider();

        Context = Provider.GetRequiredService<DddContext>();
        Context.Database.EnsureCreated();

        Context.Set<Root>()
            .AddRange(new Root("Duy"), new Root("Steven"), new Root("Hoang"), new Root("DKNet"));

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