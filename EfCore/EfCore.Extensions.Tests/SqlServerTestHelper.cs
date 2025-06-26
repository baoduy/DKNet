using Microsoft.Extensions.DependencyInjection;

namespace EfCore.Extensions.Tests;

public static class SqlServerTestHelper
{
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


    public static DbContextOptionsBuilder UseDebugLogger(this DbContextOptionsBuilder @this)
    {
        if (@this is null)
            throw new ArgumentNullException(nameof(@this));

        return @this.UseLoggerFactory(DebugLoggerFactory);
    }

    public static DbContextOptionsBuilder UseSqlServerTestContainer(this DbContextOptionsBuilder @this,
        string connectionString)
    {
        if (@this is null)
            throw new ArgumentNullException(nameof(@this));

        @this.UseSqlServer(connectionString);
        return @this;
    }
}