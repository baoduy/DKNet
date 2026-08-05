// <copyright file="IdempotencySqlServerStoreConcurrencyTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.MsSqlStore.Store;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AspCore.Idempotency.MsSqlStore.Tests.Unit;

/// <summary>
///     Proves the atomic reservation in <see cref="IdempotencySqlServerStore.IsKeyProcessedAsync" /> under real
///     concurrent connections, without Docker/SQL Server. Points the store at a file-based SQLite database — not
///     the EF Core InMemory provider, which does not enforce <c>UX_CompositeKey</c> across concurrent contexts the
///     same way a real relational database does — using the exact same <see cref="IdempotencyKeyConfiguration" />
///     the SQL Server store ships with. Mirrors the retired HTTP-level
///     <c>CreateItem_ConcurrentRequestsWithSameKey_OnlyOneProcessed</c> test (see DRK-174), but directly at the
///     store layer so it doesn't depend on SQL Server at all.
/// </summary>
public sealed class IdempotencySqlServerStoreConcurrencyTests : IAsyncLifetime
{
    #region Fields

    private readonly string _dbFilePath =
        Path.Combine(Path.GetTempPath(), $"idempotency-concurrency-{Guid.NewGuid():N}.db");

    private ServiceProvider _serviceProvider = null!;
    private IdempotencySqlServerStore _store = null!;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        await _store.DisposeAsync();
        await _serviceProvider.DisposeAsync();
        File.Delete(_dbFilePath);
    }

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<IdempotencyDbContext>(o => o.UseSqlite(
                $"Data Source={_dbFilePath}",
                // The real "Initial" migration is authored for SQL Server, so point migrations at this test
                // assembly - which has none - instead. That makes the store's own migration-check path
                // (EnsureDatabaseCreatedAsync) see nothing pending and skip straight past it; EnsureCreatedAsync
                // below builds the schema from the live model instead.
                sqlite => sqlite.MigrationsAssembly(
                    typeof(IdempotencySqlServerStoreConcurrencyTests).Assembly.GetName().Name))
            // IdempotencyKeyConfiguration hardcodes the Body column's raw type as "nvarchar(max)" (SQL Server's
            // way of saying "unbounded"); that literal string isn't valid SQLite syntax. Strip just that one
            // provider-specific override so the rest of the same configuration - crucially UX_CompositeKey -
            // builds unmodified.
            .ReplaceService<IModelCustomizer, SqliteCompatibleModelCustomizer>());
        _serviceProvider = services.BuildServiceProvider();

        var factory = _serviceProvider.GetRequiredService<IDbContextFactory<IdempotencyDbContext>>();
        await using var setupContext = await factory.CreateDbContextAsync();
        await setupContext.Database.EnsureCreatedAsync();

        _store = new IdempotencySqlServerStore(
            _serviceProvider,
            Options.Create(new IdempotencyOptions()),
            NullLogger<IdempotencySqlServerStore>.Instance);
    }

    [Fact]
    public async Task IsKeyProcessedAsync_ConcurrentRequestsWithSameKey_OnlyOneReservationWins()
    {
        // Arrange
        var keyInfo = new IdempotentKeyInfo
        {
            Endpoint = "/api/items",
            Method = "POST",
            IdempotentKey = Guid.NewGuid().ToString()
        };

        // Act - fire 5 concurrent reservation attempts for the identical composite key against the same
        // SQLite file, exactly as 5 concurrent HTTP requests would hit the same SQL Server row.
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => _store.IsKeyProcessedAsync(keyInfo).AsTask())
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert - exactly one caller wins the reservation (false, null); the unique index forces every
        // other concurrent caller onto the collision path, which reports back as already-processed/in-flight.
        results.Count(r => !r.processed).ShouldBe(1);
        results.Count(r => r.processed).ShouldBe(4);
    }

    #endregion
}

/// <summary>
///     Applies <see cref="IdempotencyKeyConfiguration" /> as-is, then drops the one column-type override in it
///     that is SQL-Server-specific raw SQL, so <c>EnsureCreatedAsync</c> can generate valid SQLite DDL.
/// </summary>
internal sealed class SqliteCompatibleModelCustomizer(ModelCustomizerDependencies dependencies)
    : ModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        var key = modelBuilder.Entity<IdempotencyKeyEntity>();
        key.Property(e => e.Body).HasColumnType(null);

        // The Sqlite provider cannot translate "> "/"<" comparisons on a DateTimeOffset column (only equality) -
        // IsKeyProcessedAsync's expiry check relies on exactly that. Store ExpiresAt as UTC ticks instead so the
        // comparison becomes an ordinary numeric one; the property's C# type/behaviour is unaffected.
        key.Property(e => e.ExpiresAt).HasConversion(
            v => v.HasValue ? v.Value.UtcTicks : (long?)null,
            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null);
    }
}
