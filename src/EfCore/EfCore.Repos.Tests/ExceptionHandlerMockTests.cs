// <copyright file="ExceptionHandlerMockTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Extensions.Extensions;
using DKNet.EfCore.Repos;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit.Abstractions;

namespace EfCore.Repos.Tests;

/// <summary>
///     Tests to verify that <see cref="IEfCoreExceptionHandler" /> properly handles
///     <see cref="DbUpdateConcurrencyException" /> when thrown from a mocked DbContext.
/// </summary>
public class ExceptionHandlerMockTests(ITestOutputHelper output)
{
    #region Methods

    [Fact]
    public async Task Repository_WhenDbContextThrowsConcurrencyException_ShouldCallExceptionHandler()
    {
        // Arrange
        // 1. Create a mock DbContext that throws DbUpdateConcurrencyException on SaveChangesAsync
        var mockDbContext = new Mock<DbContext>();

        var concurrencyException = new DbUpdateConcurrencyException(
            "Database operation expected to affect 1 row(s) but actually affected 0 row(s);",
            new InvalidOperationException());


        mockDbContext
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(concurrencyException);

        mockDbContext
            .Setup(x => x.Set<User>())
            .Returns(new Mock<DbSet<User>>().Object);

        // 2. Create a test exception handler to track invocations
        var testHandler = new ConcurrencyTestExceptionHandler();

        // 3. Add DbContext and Repository to ServiceCollection
        var services = new ServiceCollection();
        services.AddKeyedTransient<IEfCoreExceptionHandler, ConcurrencyTestExceptionHandler>(
            mockDbContext.Object.GetType().FullName,
            (_, _) => testHandler);

        var serviceProvider = services.BuildServiceProvider();

        // 4. Create repository with mocked DbContext and service provider
        var repository = new WriteRepository<User>(mockDbContext.Object, serviceProvider);

        // Act
        // Add a user and trigger SaveChangesAsync
        var user = new User("testuser") { FirstName = "Test", LastName = "User" };
        await repository.AddAsync(user);

        output.WriteLine("Attempting to save changes (should trigger exception)");
        _ = await repository.SaveChangesAsync();

        // Assert
        // 5. Ensure the ExceptionHandler.HandlingAsync got called
        testHandler.WasCalled.ShouldBeTrue();
        output.WriteLine("Handler was successfully called!");
    }

    #endregion
}

/// <summary>
///     Test implementation of <see cref="IEfCoreExceptionHandler" /> for verifying handler invocation.
/// </summary>
internal class ConcurrencyTestExceptionHandler : IEfCoreExceptionHandler
{
    #region Properties

    /// <inheritdoc />
    public int MaxRetryCount => 3;

    /// <summary>
    ///     Flag to track if HandlingAsync was called.
    /// </summary>
    public bool WasCalled { get; private set; }

    #endregion

    #region Methods

    /// <inheritdoc />
    public Task<EfConcurrencyResolution> HandlingAsync(
        DbContext context,
        DbUpdateConcurrencyException exception,
        CancellationToken cancellationToken = default)
    {
        WasCalled = true;
        return Task.FromResult(EfConcurrencyResolution.RetrySaveChanges);
    }

    #endregion
}

/// <summary>
///     Mock implementation of IEfCoreExceptionHandler for testing the handler contract.
/// </summary>
internal class MockEfCoreExceptionHandler : IEfCoreExceptionHandler
{
    #region Properties

    /// <summary>
    ///     The resolution to return (configurable for testing).
    /// </summary>
    public EfConcurrencyResolution ConfiguredResolution { get; set; } = EfConcurrencyResolution.RetrySaveChanges;

    /// <summary>
    ///     Track how many times the handler was invoked.
    /// </summary>
    public int InvokeCount { get; private set; }

    /// <summary>
    ///     The last exception that was handled.
    /// </summary>
    public DbUpdateConcurrencyException? LastException { get; private set; }

    /// <inheritdoc />
    public int MaxRetryCount => 2;

    #endregion

    #region Methods

    /// <inheritdoc />
    public Task<EfConcurrencyResolution> HandlingAsync(
        DbContext context,
        DbUpdateConcurrencyException exception,
        CancellationToken cancellationToken = default)
    {
        InvokeCount++;
        LastException = exception;
        return Task.FromResult(ConfiguredResolution);
    }

    #endregion
}

/// <summary>
///     Integration tests using mock exception handler to verify service resolution.
/// </summary>
public class ExceptionHandlerServiceResolutionTests
{
    #region Methods

    [Fact]
    public async Task MockHandler_ShouldTrackInvocations()
    {
        // Arrange
        var handler = new MockEfCoreExceptionHandler { ConfiguredResolution = EfConcurrencyResolution.IgnoreChanges };
        var mockDbContext = new Mock<DbContext>();
        var exception = new DbUpdateConcurrencyException("Test exception");

        // Act
        _ = await handler.HandlingAsync(mockDbContext.Object, exception);
        _ = await handler.HandlingAsync(mockDbContext.Object, exception);
        _ = await handler.HandlingAsync(mockDbContext.Object, exception);

        // Assert
        handler.InvokeCount.ShouldBe(3);
        handler.LastException.ShouldBe(exception);
    }

    [Fact]
    public async Task MockHandler_WithDifferentResolutions_ShouldReturnConfiguredValue()
    {
        // Arrange
        var handler = new MockEfCoreExceptionHandler();
        var mockDbContext = new Mock<DbContext>();
        var exception = new DbUpdateConcurrencyException("Test");

        // Act & Assert - Test each resolution type
        handler.ConfiguredResolution = EfConcurrencyResolution.IgnoreChanges;
        var result1 = await handler.HandlingAsync(mockDbContext.Object, exception);
        result1.ShouldBe(EfConcurrencyResolution.IgnoreChanges);

        handler.ConfiguredResolution = EfConcurrencyResolution.RetrySaveChanges;
        var result2 = await handler.HandlingAsync(mockDbContext.Object, exception);
        result2.ShouldBe(EfConcurrencyResolution.RetrySaveChanges);

        handler.ConfiguredResolution = EfConcurrencyResolution.RethrowException;
        var result3 = await handler.HandlingAsync(mockDbContext.Object, exception);
        result3.ShouldBe(EfConcurrencyResolution.RethrowException);
    }

    [Fact]
    public void ServiceProvider_ShouldResolveCustomExceptionHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddKeyedTransient<IEfCoreExceptionHandler, MockEfCoreExceptionHandler>(
            typeof(TestDbContext).FullName);

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var handler = serviceProvider.GetKeyedService<IEfCoreExceptionHandler>(typeof(TestDbContext).FullName);

        // Assert
        handler.ShouldNotBeNull();
        handler.ShouldBeOfType<MockEfCoreExceptionHandler>();
    }

    #endregion
}