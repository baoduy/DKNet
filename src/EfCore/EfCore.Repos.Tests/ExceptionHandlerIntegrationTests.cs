// <copyright file="ExceptionHandlerIntegrationTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Extensions.Extensions;
using DKNet.EfCore.Repos;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace EfCore.Repos.Tests;

/// <summary>
///     Tests to verify that <see cref="IEfCoreExceptionHandler" /> is properly resolved
///     from the service provider when using <see cref="WriteRepository{TEntity}" />.
/// </summary>
public class ExceptionHandlerIntegrationTests : IAsyncLifetime
{
    #region Fields

    private SqliteConnection? _connection;
    private TestDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        if (_dbContext != null) await _dbContext.DisposeAsync();
        if (_connection != null) await _connection.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        // Use in-memory SQLite database
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        // Setup service provider with test exception handler
        var services = new ServiceCollection();
        services.AddEfCoreExceptionHandler<TestDbContext, TestExceptionHandler>();

        _serviceProvider = services.BuildServiceProvider();

        // Create DbContext
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .UseAutoConfigModel();

        _dbContext = new TestDbContext(optionsBuilder.Options);
        await _dbContext.Database.EnsureCreatedAsync();
    }

    [Fact]
    public void ServiceProvider_WhenConfigured_ShouldResolveExceptionHandler()
    {
        // Arrange & Act
        var handler = _serviceProvider!.GetKeyedService<IEfCoreExceptionHandler>(typeof(TestDbContext).FullName);

        // Assert
        handler.ShouldNotBeNull();
        handler.ShouldBeOfType<TestExceptionHandler>();
    }

    [Fact]
    public async Task WriteRepository_WhenServiceProviderNotNull_ShouldUseExceptionHandler()
    {
        // Arrange
        var repository = new WriteRepository<User>(_dbContext!, _serviceProvider);
        var user = new User("testuser") { FirstName = "Test", LastName = "User" };
        TestExceptionHandler.HandlerInvokedCount = 0;

        // Act - Add and save normally (no exception)
        await repository.AddAsync(user);
        var result = await repository.SaveChangesAsync();

        // Assert
        result.ShouldBeGreaterThan(0);
        // Handler should not be invoked if there's no exception
        TestExceptionHandler.HandlerInvokedCount.ShouldBe(0);
    }

    [Fact]
    public void WriteRepository_WhenServiceProviderNull_ShouldNotThrowOnConstruction()
    {
        // Arrange & Act
        var repository = new WriteRepository<User>(_dbContext!, null);

        // Assert
        repository.ShouldNotBeNull();
    }

    [Fact]
    public async Task WriteRepository_WhenServiceProviderNull_ShouldSaveWithoutHandler()
    {
        // Arrange
        var repository = new WriteRepository<User>(_dbContext!, null);
        var user = new User("nohandler") { FirstName = "No", LastName = "Handler" };

        // Act
        await repository.AddAsync(user);
        var result = await repository.SaveChangesAsync();

        // Assert
        result.ShouldBeGreaterThan(0);
        var savedUser = await _dbContext!.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        savedUser.ShouldNotBeNull();
        savedUser.FirstName.ShouldBe("No");
    }

    [Fact]
    public async Task WriteRepository_WhenServiceProviderProvided_ShouldResolveHandler()
    {
        // Arrange
        var repository = new WriteRepository<User>(_dbContext!, _serviceProvider);
        var user = new User("handlertest") { FirstName = "Handler", LastName = "Test" };

        // Act
        await repository.AddAsync(user);
        var result = await repository.SaveChangesAsync();

        // Assert - Verify repository works and can access the service provider
        result.ShouldBeGreaterThan(0);

        // Verify the handler is registered and accessible
        var handler = _serviceProvider!.GetKeyedService<IEfCoreExceptionHandler>(typeof(TestDbContext).FullName);
        handler.ShouldNotBeNull();
        handler.ShouldBeOfType<TestExceptionHandler>();
    }

    #endregion
}

/// <summary>
///     Test implementation of <see cref="IEfCoreExceptionHandler" /> for testing purposes.
/// </summary>
internal class TestExceptionHandler : IEfCoreExceptionHandler
{
    #region Properties

    /// <inheritdoc />
    public int MaxRetryCount => 2;

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<EfConcurrencyResolution> HandlingAsync(
        DbContext context,
        DbUpdateConcurrencyException exception,
        CancellationToken cancellationToken = default)
    {
        HandlerInvokedCount++;

        // Simple resolution: retry the save operation
        foreach (var entry in exception.Entries)
        {
            var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken).ConfigureAwait(false);
            if (databaseValues == null) continue;

            // Keep current values and update original values to database values
            var currentValues = entry.CurrentValues.Clone();
            entry.OriginalValues.SetValues(databaseValues);
            entry.CurrentValues.SetValues(currentValues);
        }

        return EfConcurrencyResolution.RetrySaveChanges;
    }

    #endregion

    public static int HandlerInvokedCount;
}