using System;
using EfCore.TestDataLayer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EFCore.TestHelper;

public class UnitTestSetup
{
    #region Methods

    public static (MyDbContext db, IServiceProvider provider) Initialize()
    {
        provider = new ServiceCollection()
            .AddDbContext<MyDbContext>(op => op.UseInMemoryDatabase(nameof(UnitTestSetup))
                .UseLoggerFactory(SqliteMemory.DebugLoggerFactory)
                .UseAutoConfigModel(i => i.ScanFrom(typeof(MyDbContext).Assembly)))
            .AddScoped<DbContext>(op => op.GetRequiredService<MyDbContext>())
            .BuildServiceProvider();

        db = Provider.GetRequiredService<MyDbContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        return (db, provider);
    }

    #endregion Methods

    #region Fields

    private static MyDbContext db;
    private static IServiceProvider provider;

    #endregion Fields

    #region Properties

    public static MyDbContext Db => db ??= Initialize().db;

    public static IServiceProvider Provider => provider ??= Initialize().provider;

    #endregion Properties
}