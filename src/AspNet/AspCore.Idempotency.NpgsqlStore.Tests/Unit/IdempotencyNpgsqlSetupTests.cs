// <copyright file="IdempotencyNpgsqlSetupTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency.NpgsqlStore.Store;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Idempotency.NpgsqlStore.Tests.Unit;

/// <summary>
///     Tests for <see cref="IdempotencyNpgsqlSetup" />'s DI registration extension methods, in particular the
///     duplicate-registration guard on <see cref="IdempotencyDbContext" /> (DRK-466).
/// </summary>
public sealed class IdempotencyNpgsqlSetupTests
{
    #region Methods

    [Fact]
    public void AddIdempotencyNpgsqlStore_NullServices_Throws()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddIdempotencyNpgsqlStore("Host=localhost;Database=idempotency;"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddIdempotencyNpgsqlStore_NullOrWhiteSpaceConnectionString_Throws(string? connectionString)
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddIdempotencyNpgsqlStore(connectionString!));
    }

    [Fact]
    public void AddIdempotencyNpgsqlStore_CalledTwice_RegistersDbContextOnlyOnce()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - a second call, even with a different connection string, must not add a second
        // IdempotencyDbContext registration (first-wins guard).
        services.AddIdempotencyNpgsqlStore("Host=first;Database=idempotency;");
        services.AddIdempotencyNpgsqlStore("Host=second;Database=idempotency;");

        // Assert
        services.Count(s => s.ServiceType == typeof(IdempotencyDbContext)).ShouldBe(1);
    }

    [Fact]
    public void AddIdempotencyNpgsqlStore_ReturnsSameServiceCollection_ForChaining()
    {
        var services = new ServiceCollection();

        var result = services.AddIdempotencyNpgsqlStore("Host=localhost;Database=idempotency;");

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddIdempotencyWithNpgsqlStore_RegistersIdempotencyPostgresStoreAsTheKeyStore()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddIdempotencyWithNpgsqlStore("Host=localhost;Database=idempotency;");

        // Assert - checking descriptors (rather than resolving) keeps this test free of a live
        // PostgreSQL dependency, matching the pattern used by the sibling Redis-store setup tests.
        services.ShouldContain(sd => sd.ServiceType == typeof(IdempotencyDbContext));
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(DKNet.AspCore.Idempotency.Store.IIdempotencyKeyStore) &&
            sd.ImplementationType == typeof(IdempotencyPostgresStore));
    }

    #endregion
}
