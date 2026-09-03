// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: NavigationExtensionsTests.cs
// Description: Tests for NavigationExtensions, including a spike establishing whether EF Core's own
// relationship fixup already covers what AddNewEntitiesFromNavigations exists for (P22).

using DKNet.EfCore.Extensions.Extensions;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EfCore.Extensions.Tests;

/// <summary>
///     Covers <see cref="NavigationExtensions" />, including a spike (P22) that establishes whether EF Core's
///     own <c>DetectChanges</c>-driven relationship fixup already discovers and inserts new entities reachable
///     from a tracked parent's collection navigation, without the custom <c>AddNewEntitiesFromNavigations</c>
///     walk.
/// </summary>
public class NavigationExtensionsTests
{
    #region Methods

    /// <summary>
    ///     Spike for P22: adds a new child directly to a tracked parent's collection navigation and calls
    ///     <see cref="DbContext.SaveChangesAsync(CancellationToken)" /> directly - deliberately NOT going through
    ///     <c>AddNewEntitiesFromNavigations</c>/<c>GetNewEntitiesFromNavigations</c>. If EF Core's own change
    ///     tracker already discovers and inserts the child, that confirms the custom mechanism is redundant for
    ///     this (the common) case.
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_NewChildAddedToTrackedParentCollection_IsInsertedWithoutCustomMechanism()
    {
        // Arrange
        await using var context = await CreateSpikeContextAsync();
        var parent = new SpikeParent();
        context.Parents.Add(parent);
        await context.SaveChangesAsync(); // parent now tracked as Unchanged

        var child = new SpikeChild();
        parent.Children.Add(child); // reachable via navigation only - no context.Add(child), no custom walk

        // Act
        await context.SaveChangesAsync();

        // Assert: EF Core's own relationship fixup (run as part of SaveChanges' DetectChanges pass) already
        // discovered the new child through the tracked parent's collection navigation and inserted it.
        child.Id.ShouldNotBe(0);
        (await context.Set<SpikeChild>().CountAsync()).ShouldBe(1);
    }

    /// <summary>
    ///     Confirms <see cref="NavigationExtensions.GetPossibleUpdatingEntities{TDbContext}" /> no longer
    ///     considers <see cref="EntityState.Unchanged" /> entries a source of new navigation entities (P22,
    ///     option 2) - an entity nobody is saving should not be walked.
    /// </summary>
    [Fact]
    public async Task GetPossibleUpdatingEntities_UnchangedEntity_IsExcluded()
    {
        // Arrange
        await using var context = await CreateSpikeContextAsync();
        var parent = new SpikeParent();
        context.Parents.Add(parent);
        await context.SaveChangesAsync(); // parent now tracked as Unchanged, nothing pending

        // Act
        var possible = context.GetPossibleUpdatingEntities().ToList();

        // Assert
        possible.ShouldNotContain(e => ReferenceEquals(e.Entity, parent));
    }

    /// <summary>
    ///     Confirms new entities discovered under a collection navigation are still found for an entity that is
    ///     actually being saved (P22, option 2 must not break the mechanism for the Modified/Detached cases it
    ///     still serves).
    /// </summary>
    [Fact]
    public async Task GetNewEntitiesFromNavigations_ModifiedParentWithNewChild_ReturnsChild()
    {
        // Arrange
        await using var context = await CreateSpikeContextAsync();
        var parent = new SpikeParent();
        context.Parents.Add(parent);
        await context.SaveChangesAsync();

        var child = new SpikeChild();
        parent.Children.Add(child);
        context.Entry(parent).State = EntityState.Modified;

        // Act
        var newEntities = context.GetNewEntitiesFromNavigations(context.Entry(parent)).ToList();

        // Assert
        newEntities.ShouldContain(child);
    }

    /// <summary>
    ///     Confirms <see cref="EntityEntry.IsNewEntity" /> short-circuits on a Detached entry (P22, option 2)
    ///     instead of materializing its original key values.
    /// </summary>
    [Fact]
    public async Task IsNewEntity_DetachedEntity_ReturnsTrueWithoutReadingOriginalKeyValues()
    {
        // Arrange
        await using var context = await CreateSpikeContextAsync();
        var detachedChild = new SpikeChild { Id = 42 }; // key already set, but never tracked -> Detached

        // Act
        var entry = context.Entry(detachedChild);

        // Assert: Detached is decisive regardless of IsKeySet/original values.
        entry.State.ShouldBe(EntityState.Detached);
        entry.IsNewEntity().ShouldBeTrue();
    }

    private static async Task<SpikeDbContext> CreateSpikeContextAsync()
    {
        var options = new DbContextOptionsBuilder<SpikeDbContext>().UseSqlite("Data Source=:memory:").Options;
        var context = new SpikeDbContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    #endregion
}

internal sealed class SpikeParent
{
    #region Properties

    public int Id { get; set; }

    public List<SpikeChild> Children { get; set; } = [];

    #endregion
}

internal sealed class SpikeChild
{
    #region Properties

    public int Id { get; set; }

    public int ParentId { get; set; }

    #endregion
}

internal sealed class SpikeDbContext(DbContextOptions<SpikeDbContext> options) : DbContext(options)
{
    #region Properties

    public DbSet<SpikeParent> Parents => Set<SpikeParent>();

    #endregion

    #region Methods

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<SpikeParent>()
            .HasMany(p => p.Children)
            .WithOne()
            .HasForeignKey(c => c.ParentId);
    }

    #endregion
}
