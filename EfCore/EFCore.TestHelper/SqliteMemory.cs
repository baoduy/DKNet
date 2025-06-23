using System;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class SqliteMemory
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

    public static DbContextOptionsBuilder UseSqliteMemory(this DbContextOptionsBuilder @this)
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder {DataSource = ":memory:"};
        var sqliteConnection = new SqliteConnection(connectionStringBuilder.ToString());
        sqliteConnection.Open();

        @this.UseSqlite(sqliteConnection);
        return @this;
    }

    #endregion Methods
}