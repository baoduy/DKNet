using System;
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Hooks;

namespace EfCore.DataAuthorization.Tests;

public sealed class DataKeyFixture : IDisposable
{
    public DataKeyFixture()
    {
        Provider = new ServiceCollection()
            .AddLogging()
            .AddAutoDataKeyProvider<DddContext, TestDataKeyProvider>()
            .AddDbContextWithHook<DddContext>(builder =>
                builder.UseSqliteMemory()
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
    }
}