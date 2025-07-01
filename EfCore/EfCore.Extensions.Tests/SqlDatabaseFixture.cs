namespace EfCore.Extensions.Tests;

/// <summary>
/// XUnit fixture for SQL Server database setup using TestContainers.
/// This fixture is shared across all test classes that implement IClassFixture&lt;SqlDatabaseFixture&gt;.
/// </summary>
public class SqlDatabaseFixture : SqlServerTestBase, IAsyncLifetime
{
    private readonly Dictionary<string, MyDbContext> _databases = [];

    /// <summary>
    /// Initialize the SQL container when the fixture is created.
    /// </summary>
    public async Task InitializeAsync()
    {
        await StartSqlContainerAsync();
    }

    /// <summary>
    /// Cleanup resources when the fixture is disposed.
    /// </summary>
    public async Task DisposeAsync()
    {
        foreach (var db in _databases.Values)
        {
            await db.DisposeAsync();
        }
        _databases.Clear();
    }

    /// <summary>
    /// Get or create a database context for the given database name.
    /// </summary>
    public async Task<MyDbContext> GetDatabaseAsync(string dbName)
    {
        if (!_databases.TryGetValue(dbName, out var db))
        {
            await EnsureSqlStartedAsync();
            db = CreateDbContext(dbName);
            await db.Database.EnsureCreatedAsync();
            _databases[dbName] = db;
        }
        return db;
    }

    /// <summary>
    /// Get the connection string for the given database name.
    /// </summary>
    public static new string GetConnectionString(string dbName)
    {
        return SqlServerTestBase.GetConnectionString(dbName);
    }
}