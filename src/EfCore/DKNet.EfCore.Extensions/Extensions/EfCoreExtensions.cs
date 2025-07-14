// ReSharper disable CheckNamespace
namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides extension methods for Entity Framework Core operations.
/// </summary>
/// <remarks>
/// Purpose: To extend Entity Framework Core functionality with utility methods for common operations.
/// Rationale: Simplifies complex EF Core operations and provides reusable patterns for entity management.
/// 
/// Functionality:
/// - Table name resolution for entities
/// - Primary key property and value extraction
/// - Database sequence value generation with formatting support
/// - Type resolution utilities for entity mapping
/// 
/// Integration:
/// - Extends DbContext with additional utility methods
/// - Works with DKNet.EfCore.Abstractions attributes
/// - Supports SQL Server-specific features like sequences
/// 
/// Best Practices:
/// - Use sequence methods only with SQL Server provider
/// - Ensure sequence attributes are properly configured
/// - Handle null returns appropriately from utility methods
/// </remarks>
public static class EfCoreExtensions
{
    /// <summary>
    /// Gets the qualified table name for the specified entity type.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="entityType">The entity type to get the table name for.</param>
    /// <returns>The schema-qualified table name.</returns>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    internal static string GetTableName(this DbContext context, Type entityType)
    {
        ArgumentNullException.ThrowIfNull(context);
        var entity = context.Model.FindEntityType(entityType)!;
        return entity.GetSchemaQualifiedTableName()!;
    }

    /// <summary>
    /// Gets the primary key property names for the specified entity type.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="entityType">The entity type to get primary key properties for.</param>
    /// <returns>An enumerable of primary key property names.</returns>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    private static IEnumerable<string> GetPrimaryKeyProperties(this DbContext context, Type entityType)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Model.FindEntityType(entityType)?.FindPrimaryKey()?.Properties.Select(i => i.Name) ?? [];
    }

    /// <summary>
    ///     Get Primary Keys of an Entity
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="dbContext">The database context.</param>
    /// <returns>An enumerable of primary key property names.</returns>
    public static IEnumerable<string> GetPrimaryKeyProperties<TEntity>(this DbContext dbContext) =>
        dbContext.GetPrimaryKeyProperties(typeof(TEntity));

    /// <summary>
    ///     Get Primary key value of an Entity
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="entity">The entity instance.</param>
    /// <returns>An enumerable of primary key values.</returns>
    public static IEnumerable<object?> GetPrimaryKeyValues(this DbContext context, object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var type = entity.GetType();
        var keys = context.GetPrimaryKeyProperties(type);
        foreach (var key in keys)
            yield return type.GetProperty(key)?.GetValue(entity);
    }

    /// <summary>
    ///     Get the Next Sequence value
    /// </summary>
    /// <typeparam name="TEnum">The type of the enum representing the sequence.</typeparam>
    /// <typeparam name="TValue">The type of the value returned by the sequence.</typeparam>
    /// <param name="dbContext">The database context.</param>
    /// <param name="name">The name of the sequence.</param>
    /// <returns>The next value of the sequence.</returns>
    public static async ValueTask<TValue?> NextSeqValue<TEnum, TValue>(this DbContext dbContext, TEnum name)
        where TEnum : struct
        where TValue : struct =>
        (TValue?)await dbContext.NextSeqValue(name);

    /// <summary>
    /// Gets the Next Sequence value
    /// </summary>
    /// <typeparam name="TEnum">The type of the enum representing the sequence.</typeparam>
    /// <param name="dbContext">The database context.</param>
    /// <param name="name">The name of the sequence.</param>
    /// <returns>The next value of the sequence.</returns>
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", 
        Justification = "SQL is constructed from internal metadata, not user input")]
    public static async ValueTask<object?> NextSeqValue<TEnum>(this DbContext dbContext, TEnum name)
        where TEnum : struct
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var type = typeof(TEnum);
        var att = SequenceRegister.GetAttribute(type);
        if (att == null) return null;

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"SELECT NEXT VALUE FOR {att.Schema}.{SequenceRegister.GetSequenceName(name)}";

        await dbContext.Database.OpenConnectionAsync();
        await using var result = await command.ExecuteReaderAsync();

        object? rs = null;
        if (await result.ReadAsync())
            rs = await result.GetFieldValueAsync<object>(0);

        await dbContext.Database.CloseConnectionAsync();
        return rs ?? throw new InvalidOperationException($"Failed to retrieve sequence value for type: {type}");
    }

    /// <summary>
    ///     Get the Next Sequence value with FormatString defined in <see cref="SequenceAttribute" />
    /// </summary>
    /// <typeparam name="TEnum">The type of the enum representing the sequence.</typeparam>
    /// <param name="dbContext">The database context.</param>
    /// <param name="name">The name of the sequence.</param>
    /// <returns>The next value of the sequence formatted as a string.</returns>
    public static async ValueTask<string> NextSeqValueWithFormat<TEnum>(this DbContext dbContext, TEnum name)
        where TEnum : struct
    {
        var att = SequenceRegister.GetFieldAttributeOrDefault(typeof(TEnum), name);
        var value = await dbContext.NextSeqValue(name);

        if (string.IsNullOrEmpty(att.FormatString)) return $"{value}";

        var f = att.FormatString.Replace(nameof(DateTime), "0", StringComparison.OrdinalIgnoreCase);
        return string.Format(CultureInfo.CurrentCulture, f, DateTime.Now, value);
    }


    internal static Type GetEntityType(Type entityMappingType) =>
        entityMappingType.GetInterfaces().First(a => a.IsGenericType).GetGenericArguments()[0];

    internal static bool IsSequenceSupported(this DatabaseFacade database) =>
        string.Equals(database.ProviderName, "Microsoft.EntityFrameworkCore.SqlServer",
            StringComparison.OrdinalIgnoreCase);
}