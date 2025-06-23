using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace DKNet.EfCore.Relational.Helpers;

public static class DbContextHelpers
{
    public static async Task<DbConnection> GetDbConnection(this DbContext dbContext,CancellationToken cancellationToken = default)
    {
        var conn = dbContext.Database.GetDbConnection();
        if (conn.State == ConnectionState.Closed) 
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        return conn;
    }

    public static (string? schema,string? tableName) GetTableName<TEntity>(this DbContext dbContext)
    {
        var entityType = dbContext.Model.FindEntityType(typeof(TEntity));
        if (entityType == null) return (null,null);
        
        var schema = entityType.GetSchema()??entityType.GetDefaultSchema();
        var tableName = entityType.GetTableName()??entityType.GetDefaultTableName();
        return (schema, tableName);
    }
    
    /// <summary>
    /// Check whether a particular table of entity is exited or not.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TEntity"></typeparam>
    /// <returns></returns>
    public static async Task<bool> TableExistsAsync<TEntity>(this DbContext dbContext, CancellationToken cancellationToken = default) where TEntity : class
    {
        try
        {
            await dbContext.Set<TEntity>().AnyAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }
    
    /// <summary>
    /// Create Table for Entity this is not migration so you need to ensure to call this methods once only.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TEntity"></typeparam>
    public static async Task CreateTableAsync<TEntity>(this DbContext dbContext, CancellationToken cancellationToken = default) where TEntity : class
    {
        var databaseCreator = (RelationalDatabaseCreator)dbContext.Database.GetService<IDatabaseCreator>();
        if (!await databaseCreator.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            await databaseCreator.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (await dbContext.TableExistsAsync<TEntity>(cancellationToken).ConfigureAwait(false)) return;
        await databaseCreator.CreateTablesAsync(cancellationToken).ConfigureAwait(false);
    }
}