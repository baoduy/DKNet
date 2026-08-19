// <copyright file="IdempotencyMsSqlSetupTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency.MsSqlStore.Store;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Idempotency.MsSqlStore.Tests.Unit;

/// <summary>
///     Tests for <see cref="IdempotencyMsSqlSetup" />'s DI registration extension methods, in particular the
///     duplicate-registration guard on <see cref="IdempotencyDbContext" /> (DRK-466).
/// </summary>
public sealed class IdempotencyMsSqlSetupTests
{
    #region Methods

    [Fact]
    public void AddIdempotencyMsSqlStore_NullServices_Throws()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddIdempotencyMsSqlStore("Server=.;Database=Idempotency;"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddIdempotencyMsSqlStore_NullOrWhiteSpaceConnectionString_Throws(string? connectionString)
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddIdempotencyMsSqlStore(connectionString!));
    }

    [Fact]
    public void AddIdempotencyMsSqlStore_CalledTwice_RegistersDbContextOnlyOnce()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - a second call, even with a different connection string, must not add a second
        // IdempotencyDbContext registration (first-wins guard).
        services.AddIdempotencyMsSqlStore("Server=first;Database=Idempotency;");
        services.AddIdempotencyMsSqlStore("Server=second;Database=Idempotency;");

        // Assert
        services.Count(s => s.ServiceType == typeof(IdempotencyDbContext)).ShouldBe(1);
    }

    [Fact]
    public void AddIdempotencyMsSqlStore_ReturnsSameServiceCollection_ForChaining()
    {
        var services = new ServiceCollection();

        var result = services.AddIdempotencyMsSqlStore("Server=.;Database=Idempotency;");

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddIdempotencyMsSqlStore_CreatedDbContext_UsesConfiguredSqlServerOptions()
    {
        // Building (not connecting) the DbContext exercises the UseSqlServer configuration lambda -
        // migrations assembly, history table, split-query behaviour, retry-on-failure - without needing
        // a live SQL Server, keeping this test free of a Docker/Testcontainers dependency.
        var services = new ServiceCollection();
        services.AddIdempotencyMsSqlStore("Server=.;Database=Idempotency;TrustServerCertificate=True;");

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<IdempotencyDbContext>>();
        using var context = factory.CreateDbContext();

        context.Database.GetDbConnection().ConnectionString.ShouldContain("Idempotency");
    }

    [Fact]
    public void AddIdempotencyWithMsSqlStore_RegistersIdempotencySqlServerStoreAsTheKeyStore()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddIdempotencyWithMsSqlStore("Server=.;Database=Idempotency;");

        // Assert - checking descriptors (rather than resolving) keeps this test free of a live SQL
        // Server dependency, matching the pattern used by the sibling Redis-store setup tests.
        services.ShouldContain(sd => sd.ServiceType == typeof(IdempotencyDbContext));
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(DKNet.AspCore.Idempotency.Store.IIdempotencyKeyStore) &&
            sd.ImplementationType == typeof(IdempotencySqlServerStore));
    }

    #endregion
}
