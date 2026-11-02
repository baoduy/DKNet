// ReSharper disable CheckNamespace

using DKNet.EfCore.Extensions.Extensions;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
///     Provides extension methods for Entity Framework Core operations.
/// </summary>
/// <remarks>
///     Purpose: To extend Entity Framework Core functionality with utility methods for common operations.
///     Rationale: Simplifies complex EF Core operations and provides reusable patterns for entity management.
///     Functionality:
///     - Table name resolution for entities
///     - Primary key property and value extraction
///     - Database sequence value generation with formatting support
///     - Type resolution utilities for entity mapping
///     Integration:
///     - Extends DbContext with additional utility methods
///     - Works with DKNet.EfCore.Abstractions attributes
///     - Supports SQL Server-specific features like sequences
///     Best Practices:
///     - Use sequence methods only with SQL Server provider
///     - Ensure sequence attributes are properly configured
///     - Handle null returns appropriately from utility methods
/// </remarks>
public static class EfCoreExtensions
{
    #region Methods

    /// <summary>
    ///     Get Primary key value of an Entity
    /// </summary>
    /// <param name="entityEntry">The entry of the entity.</param>
    /// <returns>An enumerable of primary key values.</returns>
    public static Dictionary<string, object?> GetEntityKeyValues(this EntityEntry entityEntry)
    {
        var primaryKey = entityEntry.Metadata.FindPrimaryKey();

        if (primaryKey == null)
            // Entity does not have a primary key defined
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        return primaryKey.Properties.ToDictionary(
            p => p.Name,
            p => entityEntry.CurrentValues[p],
            StringComparer.OrdinalIgnoreCase);
    }

    internal static Type GetEntityType(Type entityMappingType)
    {
        return entityMappingType.GetInterfaces().First(a => a.IsGenericType).GetGenericArguments()[0];
    }

    #endregion

    /// <param name="context">The database context.</param>
    extension(DbContext context)
    {
        /// <summary>
        ///     Gets the primary key property names for the specified entity type.
        /// </summary>
        /// <param name="entityType">The entity type to get primary key properties for.</param>
        /// <returns>An enumerable of primary key property names.</returns>
        /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
        private IEnumerable<string> GetPrimaryKeyProperties(Type entityType)
        {
            ArgumentNullException.ThrowIfNull(context);
            return context.Model.FindEntityType(entityType)?.FindPrimaryKey()?.Properties.Select(i => i.Name) ?? [];
        }

        /// <summary>
        ///     Get Primary Keys of an Entity
        /// </summary>
        /// <typeparam name="TEntity">The type of the entity.</typeparam>
        /// <returns>An enumerable of primary key property names.</returns>
        public IEnumerable<string> GetPrimaryKeyProperties<TEntity>() =>
            context.GetPrimaryKeyProperties(typeof(TEntity));

        /// <summary>
        ///     Get Primary key value of an Entity
        /// </summary>
        /// <param name="entity">The entity instance.</param>
        /// <returns>An enumerable of primary key values.</returns>
        public IDictionary<string, object?> GetPrimaryKeyValues(object entity)
        {
            ArgumentNullException.ThrowIfNull(entity);

            var rs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var primaryKey = context.Model.FindEntityType(entity.GetType())?.FindPrimaryKey();
            if (primaryKey is null)
                return rs;

            var entry = context.Entry(entity);
            foreach (var key in primaryKey.Properties)
                rs.Add(key.Name, entry.CurrentValues[key]);

            return rs;
        }

        /// <summary>
        ///     Gets the qualified table name for the specified entity type.
        /// </summary>
        /// <param name="entityType">The entity type to get the table name for.</param>
        /// <returns>The schema-qualified table name.</returns>
        /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
        internal string GetTableName(Type entityType)
        {
            ArgumentNullException.ThrowIfNull(context);
            var entity = context.Model.FindEntityType(entityType)!;
            return entity.GetSchemaQualifiedTableName()!;
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public bool IsSqlServer() =>
            string.Equals(
                context.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.SqlServer",
                StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public bool IsNpgsql() =>
            string.Equals(
                context.Database.ProviderName,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.OrdinalIgnoreCase);

        /// <summary>
        ///     Get the Next Sequence value
        /// </summary>
        /// <typeparam name="TEnum">The type of the enum representing the sequence.</typeparam>
        /// <typeparam name="TValue">The type of the value returned by the sequence.</typeparam>
        /// <param name="name">The name of the sequence.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
        /// <returns>
        ///     The next value of the sequence, or <see langword="null" /> when <typeparamref name="TEnum" /> has no
        ///     <see cref="SqlSequenceAttribute" />.
        /// </returns>
        /// <remarks>
        ///     The provider's value is converted to <typeparamref name="TValue" /> with a checked integer or decimal
        ///     conversion,
        ///     because its runtime type depends on the provider (SQL Server returns the sequence's declared type,
        ///     Postgres always returns <see cref="long" />).
        /// </remarks>
        /// <exception cref="OverflowException">The sequence value is outside the range of <typeparamref name="TValue" />.</exception>
        /// <exception cref="InvalidCastException">
        ///     <typeparamref name="TValue" /> is an enum or not an integer or <see cref="decimal" /> type. Thrown before
        ///     the sequence is read, so no sequence value is used up.
        /// </exception>
        public async ValueTask<TValue?> NextSeqValue<TEnum, TValue>(TEnum name, CancellationToken cancellationToken = default)
            where TEnum : struct
            where TValue : struct
        {
            // Convert.ChangeType also accepts bool, char and floating-point targets, which would silently
            // reinterpret or round the sequence value, and rejects enums; only integer and decimal targets get an
            // exact, checked conversion. Checked before the read so a rejected TValue uses up no sequence value.
            if (typeof(TValue).IsEnum || Type.GetTypeCode(typeof(TValue)) is not (TypeCode.SByte or TypeCode.Byte
                    or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64
                    or TypeCode.UInt64 or TypeCode.Decimal))
                throw new InvalidCastException(
                    $"Sequence values cannot be converted to {typeof(TValue)}; use an integer or decimal type.");

            var value = await context.NextSeqValue(name, cancellationToken);
            return value is null
                ? null
                : (TValue)Convert.ChangeType(value, typeof(TValue), CultureInfo.InvariantCulture);
        }

        /// <summary>
        ///     Gets the Next Sequence value
        /// </summary>
        /// <typeparam name="TEnum">The type of the enum representing the sequence.</typeparam>
        /// <param name="name">The name of the sequence.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
        /// <returns>The next value of the sequence.</returns>
        /// <exception cref="NotSupportedException">Thrown when the database provider is neither SQL Server nor Npgsql.</exception>
        [SuppressMessage(
            "Security",
            "CA2100:Review SQL queries for security vulnerabilities",
            Justification = "The sequence schema/name come from internal [Sequence] metadata, not user input. " +
                             "Neither T-SQL's NEXT VALUE FOR nor this shared code path can parameterize a sequence " +
                             "identifier, so it must be interpolated into the command text.")]
        public async ValueTask<object?> NextSeqValue<TEnum>(TEnum name, CancellationToken cancellationToken = default)
            where TEnum : struct
        {
            ArgumentNullException.ThrowIfNull(context);

            var type = typeof(TEnum);
            var att = SequenceExtensions.GetAttribute(type);
            if (att == null) return null;

            var sequenceName = SequenceExtensions.GetSequenceName(name);

            await using var command = context.Database.GetDbConnection().CreateCommand();

            // SQL Server refuses a command on a connection with a pending local transaction unless the command is
            // enlisted in it, so join EF's current transaction (null when there is none).
            command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = context.IsSqlServer()
                ? $"SELECT NEXT VALUE FOR {att.Schema}.{sequenceName}"
                : context.IsNpgsql()
                    ? $"SELECT nextval('{att.Schema}.\"{sequenceName}\"')"
                    : throw new NotSupportedException(
                        $"Provider '{context.Database.ProviderName}' is not supported for sequence value generation.");

            // Use EF Core's own (reference-counted, transaction-aware) connection management, and always release it
            // via `finally` - an unhandled exception between Open and Close must not leak the open reference and
            // leave the caller's ambient connection/transaction stuck.
            await context.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                var rs = await command.ExecuteScalarAsync(cancellationToken);
                return rs ?? throw new InvalidOperationException($"Failed to retrieve sequence value for type: {type}");
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }
        }

        /// <summary>
        ///     Get the Next Sequence value with FormatString defined in <see cref="SequenceAttribute" />
        /// </summary>
        /// <typeparam name="TEnum">The type of the enum representing the sequence.</typeparam>
        /// <param name="name">The name of the sequence.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
        /// <returns>The next value of the sequence formatted as a string.</returns>
        public async ValueTask<string> NextSeqValueWithFormat<TEnum>(TEnum name, CancellationToken cancellationToken = default)
            where TEnum : struct
        {
            var att = SequenceExtensions.GetFieldAttributeOrDefault(typeof(TEnum), name);
            var value = await context.NextSeqValue(name, cancellationToken);

            if (string.IsNullOrEmpty(att.FormatString)) return $"{value}";

            var f = att.FormatString.Replace(nameof(DateTime), "0", StringComparison.OrdinalIgnoreCase);
            // Use UTC and invariant culture so generated sequence codes are deterministic regardless of server
            // time zone or the running culture.
            return string.Format(CultureInfo.InvariantCulture, f, DateTime.UtcNow, value);
        }
    }
}