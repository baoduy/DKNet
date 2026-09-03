// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace DKNet.EfCore.Relational.Helpers;

/// <summary>
///     Provides helper methods for working with Entity Framework Core DbContext.
/// </summary>
public static class DbContextHelpers
{
    #region Methods

    /// <summary>
    ///     Ensures the database exists and creates <b>every</b> table declared in the <see cref="DbContext" />'s
    ///     model — not only the table for <typeparamref name="TEntity" />. This is not a migration; ensure this
    ///     method is called only once, against a database that has none of the model's tables yet.
    /// </summary>
    /// <remarks>
    ///     <typeparamref name="TEntity" /> only gates whether creation runs at all: if its table already exists,
    ///     the whole-model creation step is skipped, even for other tables in the model that do not exist yet. It
    ///     does not create <typeparamref name="TEntity" />'s table in isolation — EF Core's
    ///     <see cref="RelationalDatabaseCreator.CreateTablesAsync" /> has no per-table mode, it creates the whole
    ///     schema in one pass.
    /// </remarks>
    /// <param name="dbContext">The <see cref="DbContext" /> instance to operate on.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <typeparam name="TEntity">The entity type whose presence gates whether the whole schema gets created.</typeparam>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public static async Task CreateTableAsync<TEntity>(
        this DbContext dbContext,
        CancellationToken cancellationToken = default)
        where TEntity : class
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

    /// <summary>
    ///     Gets the database connection from the DbContext, opening it if closed.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>An open database connection.</returns>
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

    /// <summary>
    ///     Gets the schema and table name for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="dbContext">The database context.</param>
    /// <returns>A tuple containing the <c>Schema</c> and <c>TableName</c>, or null values if the entity is not found.</returns>
    public static (string? Schema, string? TableName) GetTableName<TEntity>(this DbContext dbContext)
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

    /// <summary>
    ///     Checks whether a particular table for the specified entity exists in the database, using the provider's
    ///     <c>INFORMATION_SCHEMA.TABLES</c> catalog view (supported by both SQL Server and PostgreSQL).
    /// </summary>
    /// <param name="dbContext">The <see cref="DbContext" /> instance to operate on.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <typeparam name="TEntity">The entity type to check for table existence.</typeparam>
    /// <returns><c>true</c> if the table exists; otherwise, <c>false</c>.</returns>
    public static async Task<bool> TableExistsAsync<TEntity>(
        this DbContext dbContext,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var (schema, tableName) = dbContext.GetTableName<TEntity>();
        if (tableName is null)
            throw new InvalidOperationException(
                $"Entity type '{typeof(TEntity)}' is not part of the model for context '{dbContext.GetType().Name}'.");

        // SingleAsync() composes over this query (adds a row-limiting wrapper), so per EF Core's SqlQuery<T>
        // contract the projected column must be named "Value".
        var query = schema is null
            ? dbContext.Database.SqlQuery<int>(
                $"SELECT COUNT(*) AS \"Value\" FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = {tableName}")
            : dbContext.Database.SqlQuery<int>(
                $"SELECT COUNT(*) AS \"Value\" FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = {tableName} AND TABLE_SCHEMA = {schema}");

        var count = await query.SingleAsync(cancellationToken);
        return count > 0;
    }

    #endregion
}