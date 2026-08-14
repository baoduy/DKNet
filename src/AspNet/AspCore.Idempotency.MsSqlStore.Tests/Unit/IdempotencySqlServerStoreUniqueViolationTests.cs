// <copyright file="IdempotencySqlServerStoreUniqueViolationTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Reflection;
using DKNet.AspCore.Idempotency.MsSqlStore.Store;
using Microsoft.Data.SqlClient;

namespace AspCore.Idempotency.MsSqlStore.Tests.Unit;

/// <summary>
///     Proves <see cref="IdempotencySqlServerStore" />'s private <c>IsUniqueViolation</c> helper classifies by
///     <see cref="SqlException.Number" /> rather than by the server's localized error message. Neither the
///     SQLite-backed <see cref="IdempotencySqlServerStoreConcurrencyTests" /> nor
///     <see cref="IdempotencySqlServerStoreLifecycleTests" /> can exercise this: SQLite never throws a
///     <see cref="SqlException" />, so this is the only test that actually pins the locale-independence
///     behaviour DRK-324 fixes. <see cref="SqlException" /> has no public constructor, so instances are
///     synthesized via <see cref="SqlException.CreateException(SqlErrorCollection, string)" /> — the same
///     internal factory the SqlClient driver itself uses — reached through reflection.
/// </summary>
public sealed class IdempotencySqlServerStoreUniqueViolationTests
{
    #region Methods

    /// <summary>
    ///     Builds a <see cref="SqlException" /> carrying a single <see cref="SqlError" /> with the given
    ///     <paramref name="number" />, using the driver's own (internal) construction path.
    /// </summary>
    private static SqlException CreateSqlException(int number)
    {
        var sqlClientAssembly = typeof(SqlException).Assembly;
        var errorType = sqlClientAssembly.GetType("Microsoft.Data.SqlClient.SqlError", throwOnError: true)!;
        var errorCollectionType =
            sqlClientAssembly.GetType("Microsoft.Data.SqlClient.SqlErrorCollection", throwOnError: true)!;

        var error = Activator.CreateInstance(errorType, BindingFlags.NonPublic | BindingFlags.Instance, null,
            [number, (byte)0, (byte)0, "test-server", "synthetic error", "test-procedure", 0, null], null)!;

        var errorCollection = Activator.CreateInstance(errorCollectionType, nonPublic: true)!;
        errorCollectionType.GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(errorCollection, [error]);

        var createException = typeof(SqlException).GetMethod("CreateException",
            BindingFlags.NonPublic | BindingFlags.Static, null, [errorCollectionType, typeof(string)], null)!;

        return (SqlException)createException.Invoke(null, [errorCollection, "7.0"])!;
    }

    /// <summary>
    ///     Invokes the private static <c>IsUniqueViolation(DbUpdateException)</c> helper directly.
    /// </summary>
    private static bool InvokeIsUniqueViolation(DbUpdateException ex)
    {
        var method = typeof(IdempotencySqlServerStore).GetMethod("IsUniqueViolation",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (bool)method.Invoke(null, [ex])!;
    }

    [Theory]
    [InlineData(2601)] // duplicate key in a unique index (UX_CompositeKey)
    [InlineData(2627)] // unique/primary key constraint violation
    public void IsUniqueViolation_SqlExceptionWithUniqueViolationNumber_ReturnsTrue(int errorNumber)
    {
        // Arrange - a SqlException carrying the driver's own error number, independent of any localized message
        var sqlException = CreateSqlException(errorNumber);
        var dbUpdateException = new DbUpdateException("Save failed.", sqlException);

        // Act
        var result = InvokeIsUniqueViolation(dbUpdateException);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsUniqueViolation_SqlExceptionWithNonMatchingNumber_ReturnsFalse()
    {
        // Arrange - error 547 is a constraint violation (e.g. foreign key), not a unique-key collision
        var sqlException = CreateSqlException(547);
        var dbUpdateException = new DbUpdateException("Save failed.", sqlException);

        // Act
        var result = InvokeIsUniqueViolation(dbUpdateException);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsUniqueViolation_InnerExceptionNotSqlException_ReturnsFalse()
    {
        // Arrange - a non-SqlException inner exception (e.g. any other provider) must never be misclassified
        var dbUpdateException = new DbUpdateException("Save failed.", new InvalidOperationException("boom"));

        // Act
        var result = InvokeIsUniqueViolation(dbUpdateException);

        // Assert
        result.ShouldBeFalse();
    }

    #endregion
}
