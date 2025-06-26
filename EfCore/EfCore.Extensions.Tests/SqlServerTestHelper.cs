using Microsoft.Extensions.DependencyInjection;

namespace EfCore.Extensions.Tests;

public static class SqlServerTestHelper
{
    #region Properties

    public static LoggerFactory DebugLoggerFactory =>
        new([
            new DebugLoggerProvider(),
        ], new LoggerFilterOptions
        {
            Rules =
            {
                new LoggerFilterRule("EfCoreDebugger", string.Empty,
                    LogLevel.Trace, (m, n, l) => m.Contains("Query", StringComparison.OrdinalIgnoreCase))
            },
        });

    #endregion Properties

    #region Methods

    public static DbContextOptionsBuilder UseDebugLogger(this DbContextOptionsBuilder @this)
    {
        if (@this is null)
            throw new ArgumentNullException(nameof(@this));

        return @this.UseLoggerFactory(DebugLoggerFactory);
    }

    public static DbContextOptionsBuilder UseSqlServerTestContainer(this DbContextOptionsBuilder @this, string connectionString)
    {
        if (@this is null)
            throw new ArgumentNullException(nameof(@this));

        @this.UseSqlServer(connectionString);
        return @this;
    }

    #endregion Methods
}

public class SqlServerTestSetup
{
    #region Methods

    public static (MyDbContext db, IServiceProvider provider) Initialize(string connectionString)
    {
        var provider = new ServiceCollection()
            .AddDbContext<MyDbContext>(op => op.UseSqlServer(connectionString)
                .UseLoggerFactory(SqlServerTestHelper.DebugLoggerFactory)
                .UseAutoConfigModel(i => i.ScanFrom(typeof(MyDbContext).Assembly)))
            .AddScoped<DbContext>(op => op.GetRequiredService<MyDbContext>())
            .BuildServiceProvider();

        var db = provider.GetRequiredService<MyDbContext>();
        db.Database.EnsureCreated();

        return (db, provider);
    }

    #endregion Methods
}