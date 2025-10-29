using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace DKNet.EfCore.Relational.Helpers;

public static class DbContextHelpers
{
    #region Methods

    /// <summary>
    ///     Create Table for Entity this is not migration so you need to ensure to call this methods once only.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TEntity"></typeparam>
    public static async Task CreateTableAsync<TEntity>(
        this DbContext dbContext,
        CancellationToken cancellationToken = default) where TEntity : class
    {
        var databaseCreator = (RelationalDatabaseCreator)dbContext.Database.GetService<IDatabaseCreator>();
        if (!await databaseCreator.ExistsAsync(cancellationToken))
        {
            await databaseCreator.EnsureCreatedAsync(cancellationToken);
        }

        if (await dbContext.TableExistsAsync<TEntity>(cancellationToken))
        {
            return;
        }

        await databaseCreator.CreateTablesAsync(cancellationToken);
    }

    public static async Task<DbConnection> GetDbConnection(
        this DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var conn = dbContext.Database.GetDbConnection();
        if (conn.State == ConnectionState.Closed)
        {
            await conn.OpenAsync(cancellationToken);
        }

        return conn;
    }

    public static (string? schema, string? tableName) GetTableName<TEntity>(this DbContext dbContext)
    {
        var defaultSchema = dbContext.IsSqlServer() ? "dbo" : null;

        var entityType = dbContext.Model.FindEntityType(typeof(TEntity));
        if (entityType == null)
        {
            return (null, null);
        }

        var schema = entityType.GetSchema() ?? entityType.GetDefaultSchema() ?? defaultSchema;
        var tableName = entityType.GetTableName() ?? entityType.GetDefaultTableName();
        return (schema, tableName);
    }

    private static bool IsSqlServer(this DbContext context) =>
        string.Equals(
            context.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.SqlServer",
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Check whether a particular table of entity is exited or not.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TEntity"></typeparam>
    /// <returns></returns>
    public static async Task<bool> TableExistsAsync<TEntity>(
        this DbContext dbContext,
        CancellationToken cancellationToken = default) where TEntity : class
    {
        try
        {
            await dbContext.Set<TEntity>().AnyAsync(cancellationToken);
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }

    #endregion
}