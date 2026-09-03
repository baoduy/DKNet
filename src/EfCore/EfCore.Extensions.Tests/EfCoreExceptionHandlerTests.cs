// <copyright file="EfCoreExceptionHandlerTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Extensions.Extensions;

namespace EfCore.Extensions.Tests;

/// <summary>
///     Covers the non-recoverable branch of <see cref="EfCoreExceptionHandler.HandlingAsync" />: a conflict that
///     carries no entries is not safe to retry, so it must rethrow. The recoverable branch (a conflict that
///     carries entries) needs a genuine <see cref="DbUpdateConcurrencyException" /> from a real provider -
///     <c>DbUpdateConcurrencyException.Entries</c> wraps an internal EF Core type that cannot be constructed
///     from a test double - so it is covered against Postgres in
///     <see cref="WithSqlDbTests.HandlingAsync_WithRealConcurrencyConflict_ReloadsValuesAndReturnsRetrySaveChanges" />.
/// </summary>
public class EfCoreExceptionHandlerTests
{
    #region Methods

    [Fact]
    public async Task HandlingAsync_WhenExceptionHasNoEntries_ReturnsRethrowException()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase("EfCoreExceptionHandler_NonRecoverable_" + Guid.NewGuid())
            .Options;
        await using var context = new MyDbContext(options);

        var exception = new DbUpdateConcurrencyException("simulated - not the affected-zero-rows case", []);
        var handler = new EfCoreExceptionHandler();

        // Act
        var result = await handler.HandlingAsync(context, exception);

        // Assert
        result.ShouldBe(EfConcurrencyResolution.RethrowException);
    }

    #endregion
}
