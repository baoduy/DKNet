// <copyright file="DataSeedingEdgeCaseTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Extensions.Configurations;

namespace EfCore.Extensions.Tests;

/// <summary>
///     Covers the early-return branches in <see cref="DataSeedingConfiguration{TEntity}.SeedAsync" />
///     that the happy-path <see cref="DataSeedingTests" /> does not exercise:
///     (1) seed data collection is empty, (2) all entities already exist in the database.
/// </summary>
public class DataSeedingEdgeCaseTests
{
    #region Empty-data branch

    private sealed class EmptyUserSeedingConfiguration : DataSeedingConfiguration<User>
    {
        protected override ValueTask<ICollection<User>> GetDataAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<ICollection<User>>([]);
    }

    [Fact]
    public async Task SeedAsync_WhenGetDataReturnsEmpty_DoesNothing()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase("DataSeedingEdge_Empty_" + Guid.NewGuid())
            .Options;
        await using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var config = new EmptyUserSeedingConfiguration();

        // Act: invoke SeedAsync directly (matches the runtime contract used by UseAutoDataSeeding)
        await config.SeedAsync(context, false, CancellationToken.None);

        // Assert: empty seed must not add any rows
        (await context.Set<User>().CountAsync()).ShouldBe(0);
    }

    #endregion

    #region All-exist branch

    // Custom entity with value equality so the production code's HashSet<TEntity> with
    // EqualityComparer<TEntity>.Default treats AsNoTracking-loaded copies as the same as the
    // seed candidates and the early-return at "toAdd.Count == 0" fires.
    private sealed class ColorRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public override bool Equals(object? obj) => obj is ColorRow other && other.Id == Id;
        public override int GetHashCode() => Id.GetHashCode();
    }

    private sealed class ColorDbContext(DbContextOptions<ColorDbContext> options) : DbContext(options)
    {
        public DbSet<ColorRow> Colors => Set<ColorRow>();
    }

    private sealed class FixedColorSeedingConfiguration : DataSeedingConfiguration<ColorRow>
    {
        public static IReadOnlyList<ColorRow> Rows { get; set; } = [];

        protected override ValueTask<ICollection<ColorRow>> GetDataAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<ICollection<ColorRow>>(Rows.ToList());
    }

    [Fact]
    public async Task SeedAsync_WhenAllEntitiesAlreadyExist_SkipsAddRange()
    {
        // Arrange
        var dbName = "DataSeedingEdge_AllExist_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ColorDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var seedRows = new[]
        {
            new ColorRow { Id = 101, Name = "Red" },
            new ColorRow { Id = 102, Name = "Blue" }
        };

        // Pre-populate with rows that compare-equal to the seed candidates.
        await using (var seedContext = new ColorDbContext(options))
        {
            await seedContext.Database.EnsureCreatedAsync();
            seedContext.AddRange(seedRows.Select(r => new ColorRow { Id = r.Id, Name = r.Name }));
            await seedContext.SaveChangesAsync();
        }

        await using var context = new ColorDbContext(options);
        FixedColorSeedingConfiguration.Rows = seedRows;
        var config = new FixedColorSeedingConfiguration();

        // Act — invoking SeedAsync should NOT insert any duplicates because every candidate is
        // already in the existing set, hitting the `toAdd.Count == 0` early-return.
        await config.SeedAsync(context, false, CancellationToken.None);

        // Assert
        (await context.Set<ColorRow>().CountAsync()).ShouldBe(2);
    }

    #endregion

    #region Properties

    [Fact]
    public void Defaults_OrderIsZeroAndEntityTypeMatchesGenericArgument()
    {
        IDataSeedingConfiguration config = new EmptyUserSeedingConfiguration();

        config.Order.ShouldBe(0);
        config.EntityType.ShouldBe(typeof(User));
        config.SeedAsync.ShouldNotBeNull();
    }

    #endregion
}
