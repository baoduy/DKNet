// <copyright file="RepositorySpecExceptionHandlerMockTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Extensions.Extensions;
using MapsterMapper;
using Xunit.Abstractions;

namespace EfCore.Specifications.Tests;

/// <summary>
///     Tests to verify that <see cref="IEfCoreExceptionHandler"/> properly handles
///     <see cref="DbUpdateConcurrencyException"/> when thrown from a mocked DbContext in RepositorySpec.
/// </summary>
public class RepositorySpecExceptionHandlerMockTests(ITestOutputHelper output)
{
    #region Methods

    [Fact]
    public async Task RepositorySpec_WhenDbContextThrowsConcurrencyException_ShouldCallExceptionHandler()
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
            .Setup(x => x.Set<Product>())
            .Returns(new Mock<DbSet<Product>>().Object);

        // 2. Create a test exception handler to track invocations
        var testHandler = new RepositorySpecTestExceptionHandler();

        // 3. Add DbContext and RepositorySpec to ServiceCollection
        var services = new ServiceCollection();
        services.AddKeyedTransient<IEfCoreExceptionHandler, RepositorySpecTestExceptionHandler>(
            mockDbContext.Object.GetType().FullName,
            (_, _) => testHandler);

        var serviceProvider = services.BuildServiceProvider();

        // Create a mapper instance
        var mapper = new Mapper(new Mapster.TypeAdapterConfig());

        // 4. Create repository with mocked DbContext and service provider
        var repository = new RepositorySpec<DbContext>(mockDbContext.Object, [mapper], serviceProvider);

        // Act
        // Add a product and trigger SaveChangesAsync
        var product = new Product { Name = "TestProduct", Price = 99.99m, IsActive = true, CategoryId = 1 };
        await repository.AddAsync(product);

        output.WriteLine("Attempting to save changes on RepositorySpec (should trigger exception)");
        _ = await repository.SaveChangesAsync();

        // Assert
        // 5. Ensure the ExceptionHandler.HandlingAsync got called
        testHandler.WasCalled.ShouldBeTrue();
        output.WriteLine("RepositorySpec exception handler was successfully called!");
    }

    [Fact]
    public void RepositorySpec_WithServiceProvider_ShouldResolveHandler()
    {
        // Arrange
        var mockDbContext = new Mock<DbContext>();
        var testHandler = new RepositorySpecTestExceptionHandler();

        var services = new ServiceCollection();
        services.AddKeyedTransient<IEfCoreExceptionHandler, RepositorySpecTestExceptionHandler>(
            mockDbContext.Object.GetType().FullName,
            (_, _) => testHandler);

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var resolvedHandler = serviceProvider.GetKeyedService<IEfCoreExceptionHandler>(
            mockDbContext.Object.GetType().FullName);

        // Assert
        resolvedHandler.ShouldNotBeNull();
        resolvedHandler.ShouldBe(testHandler);
        output.WriteLine("RepositorySpec exception handler was correctly resolved from service provider!");
    }

    [Fact]
    public async Task RepositorySpec_WithNullServiceProvider_ShouldNotThrow()
    {
        // Arrange
        var mockDbContext = new Mock<DbContext>();
        var mapper = new Mapper(new Mapster.TypeAdapterConfig());

        mockDbContext
            .Setup(x => x.Set<Product>())
            .Returns(new Mock<DbSet<Product>>().Object);

        // Act & Assert - Should not throw even with null provider
        var repository = new RepositorySpec<DbContext>(mockDbContext.Object, [mapper], provider: null);
        var product = new Product { Name = "TestProduct", Price = 99.99m, IsActive = true, CategoryId = 1 };

        await Should.NotThrowAsync(async () =>
        {
            await repository.AddAsync(product);
        });

        output.WriteLine("RepositorySpec handles null service provider correctly!");
    }

    #endregion
}

/// <summary>
///     Test implementation of <see cref="IEfCoreExceptionHandler"/> for RepositorySpec testing.
/// </summary>
internal class RepositorySpecTestExceptionHandler : IEfCoreExceptionHandler
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
///     Mock implementation of IEfCoreExceptionHandler for RepositorySpec contract testing.
/// </summary>
internal class MockRepositorySpecExceptionHandler : IEfCoreExceptionHandler
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
///     Integration tests using mock exception handler to verify RepositorySpec service resolution.
/// </summary>
public class RepositorySpecExceptionHandlerServiceResolutionTests
{
    #region Methods

    [Fact]
    public async Task MockHandler_ShouldTrackInvocationsForRepositorySpec()
    {
        // Arrange
        var handler = new MockRepositorySpecExceptionHandler { ConfiguredResolution = EfConcurrencyResolution.IgnoreChanges };
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
    public async Task MockHandler_WithDifferentResolutions_ShouldReturnConfiguredValueForRepositorySpec()
    {
        // Arrange
        var handler = new MockRepositorySpecExceptionHandler();
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
    public void RepositorySpec_ServiceProvider_ShouldResolveCustomExceptionHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddKeyedTransient<IEfCoreExceptionHandler, MockRepositorySpecExceptionHandler>(
            typeof(DbContext).FullName);

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var handler = serviceProvider.GetKeyedService<IEfCoreExceptionHandler>(typeof(DbContext).FullName);

        // Assert
        handler.ShouldNotBeNull();
        handler.ShouldBeOfType<MockRepositorySpecExceptionHandler>();
    }

    #endregion
}
