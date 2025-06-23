using System.Globalization;
using DKNet.EfCore.Abstractions.Attributes;
using Microsoft.EntityFrameworkCore.Infrastructure;
using DKNet.EfCore.Extensions.Registers;

// ReSharper disable CheckNamespace
namespace Microsoft.EntityFrameworkCore;

public static class EfCoreExtensions
{
    internal static string GetTableName(this DbContext context, Type entityType)
    {
        ArgumentNullException.ThrowIfNull(context);
        var entity = context.Model.FindEntityType(entityType)!;
        return entity.GetSchemaQualifiedTableName()!;
    }

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

        var keys = context.GetPrimaryKeyProperties(entity.GetType());
        foreach (var key in keys)
            yield return entity.GetType().GetProperty(key)?.GetValue(entity);
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
        (TValue?)await dbContext.NextSeqValue(name).ConfigureAwait(false);

    /// <summary>
    ///     Get the Next Sequence value
    /// </summary>
    /// <typeparam name="TEnum">The type of the enum representing the sequence.</typeparam>
    /// <param name="dbContext">The database context.</param>
    /// <param name="name">The name of the sequence.</param>
    /// <returns>The next value of the sequence.</returns>
    public static async ValueTask<object?> NextSeqValue<TEnum>(this DbContext dbContext, TEnum name)
        where TEnum : struct
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var type = typeof(TEnum);
        var att = SequenceRegister.GetAttribute(type);
        if (att == null) return null;

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"SELECT NEXT VALUE FOR {att.Schema}.{SequenceRegister.GetSequenceName(name)}";

        await dbContext.Database.OpenConnectionAsync().ConfigureAwait(false);
        await using var result = await command.ExecuteReaderAsync().ConfigureAwait(false);

        object? rs = null;
        if (await result.ReadAsync().ConfigureAwait(false))
            rs = await result.GetFieldValueAsync<object>(0);

        await dbContext.Database.CloseConnectionAsync();
        return rs ?? throw new InvalidOperationException(type.ToString());
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
        var value = await dbContext.NextSeqValue(name).ConfigureAwait(false);

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