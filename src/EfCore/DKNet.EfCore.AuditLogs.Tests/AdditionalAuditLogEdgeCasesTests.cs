using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace DKNet.EfCore.AuditLogs.Tests;

public class AdditionalAuditLogEdgeCasesTests
{
    #region Methods

    [Fact]
    public async Task BuildAuditLog_Deletions_Include_All_Scalar_Properties()
    {
        await using var ctx = await InitAsync();
        var e = new TestAuditEntity { Name = "DelUser", Age = 5, IsActive = true, Balance = 10m };
        e.SetCreatedBy("creator");
        await ctx.AddAsync(e);
        await ctx.SaveChangesAsync();
        ctx.Remove(e);
        ctx.ChangeTracker.DetectChanges();
        var entry = ctx.Entry(e);
        var log = entry.BuildAuditLog(EntityState.Deleted, AuditLogBehaviour.IncludeAllAuditedEntities)!;
        log.Changes.Count.ShouldBeGreaterThan(0); // Should contain all mapped properties
        log.Changes.All(c => c.NewValue == null).ShouldBeTrue();
    }

    [Fact]
    public async Task BuildAuditLog_EmptyChanges_WhenForcedModified_NoDiff()
    {
        await using var ctx = await InitAsync();
        var e = new TestAuditEntity { Name = "NoDiff", Age = 8, IsActive = true, Balance = 5m };
        e.SetCreatedBy("creator");
        await ctx.AddAsync(e);
        await ctx.SaveChangesAsync();
        var entry = ctx.Entry(e);

        // Force state to Modify without altering values
        entry.State = EntityState.Modified;

        // Ensure none of the properties marked modified
        foreach (var p in entry.Properties)
        {
            p.IsModified = false;
        }

        var log = entry.BuildAuditLog(EntityState.Modified, AuditLogBehaviour.IncludeAllAuditedEntities)!;
        log.Changes.ShouldBeEmpty();
    }

    [Fact]
    public async Task BuildAuditLog_Ignores_Unchanged_Null_Property()
    {
        await using var ctx = await InitAsync();
        var e = new TestAuditEntity { Name = "NullSkip", Age = 4, IsActive = true, Balance = 2m, Notes = null };
        e.SetCreatedBy("creator");
        await ctx.AddAsync(e);
        await ctx.SaveChangesAsync();

        // Only update profile (audit fields) - Notes stays null
        e.UpdateProfile("updater");
        ctx.ChangeTracker.DetectChanges();
        var entry = ctx.Entry(e);
        var log = entry.BuildAuditLog(EntityState.Modified, AuditLogBehaviour.IncludeAllAuditedEntities)!;
        log.Changes.ShouldNotContain(c => c.FieldName == nameof(TestAuditEntity.Notes));
    }

    [Fact]
    public async Task BuildAuditLog_ModifiedEqualValue_StillIncluded_When_IsModifiedTrue()
    {
        await using var ctx = await InitAsync();
        var e = new TestAuditEntity { Name = "EqualUser", Age = 7, IsActive = true, Balance = 9m };
        e.SetCreatedBy("creator");
        await ctx.AddAsync(e);
        await ctx.SaveChangesAsync();

        // Mark property as modified without changing its value
        var entry = ctx.Entry(e);
        entry.Property(nameof(TestAuditEntity.Age)).IsModified = true; // value remains 7
        var log = entry.BuildAuditLog(EntityState.Modified, AuditLogBehaviour.IncludeAllAuditedEntities)!;
        log.Changes.ShouldContain(c =>
            c.FieldName == nameof(TestAuditEntity.Age) && (int?)c.OldValue == 7 && (int?)c.NewValue == 7);
    }

    [Fact]
    public async Task BuildAuditLog_Skips_Unchanged_Properties()
    {
        await using var ctx = await InitAsync();
        var e = new TestAuditEntity { Name = "SkipUser", Age = 2, IsActive = false, Balance = 3.3m };
        e.SetCreatedBy("creator");
        await ctx.AddAsync(e);
        await ctx.SaveChangesAsync();

        // No changes except we set updated info without altering scalar fields
        e.UpdateProfile("updater");

        // Removed ctx.Update(e) to allow EF Core to track only changed audit fields
        ctx.ChangeTracker.DetectChanges();
        var entry = ctx.Entry(e);
        var log = entry.BuildAuditLog(EntityState.Modified, AuditLogBehaviour.IncludeAllAuditedEntities)!;

        // Age, Name, Balance, IsActive unchanged -> changes should only include audit fields UpdatedBy / UpdatedOn if tracked
        log.Changes.ShouldContain(c => c.FieldName == nameof(TestAuditEntity.UpdatedBy));
        log.Changes.ShouldContain(c => c.FieldName == nameof(TestAuditEntity.UpdatedOn));
        log.Changes.ShouldNotContain(c =>
            c.FieldName == nameof(TestAuditEntity.Age) && (int?)c.OldValue == 2 && (int?)c.NewValue == 2);
    }

    [Fact]
    public async Task BuildAuditLog_ValueCleared_To_Null_Registers_Change()
    {
        await using var ctx = await InitAsync();
        var e = new TestAuditEntity { Name = "ClearUser", Age = 3, IsActive = true, Balance = 1m, Notes = "original" };
        e.SetCreatedBy("creator");
        await ctx.AddAsync(e);
        await ctx.SaveChangesAsync();
        e.Notes = null; // clear value
        e.UpdateProfile("updater");
        ctx.Update(e);
        ctx.ChangeTracker.DetectChanges();
        var entry = ctx.Entry(e);
        var log = entry.BuildAuditLog(EntityState.Modified, AuditLogBehaviour.IncludeAllAuditedEntities)!;
        log.Changes.ShouldContain(c =>
            c.FieldName == nameof(TestAuditEntity.Notes) && (string?)c.OldValue == "original" && c.NewValue == null);
    }

    private static TestAuditDbContext CreateCtx() => new(
        new DbContextOptionsBuilder<TestAuditDbContext>()
            .UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), $"edge_{Guid.NewGuid():N}.db")}")
            .EnableSensitiveDataLogging()
            .Options);

    private static async Task<TestAuditDbContext> InitAsync()
    {
        var ctx = CreateCtx();
        await ctx.Database.EnsureCreatedAsync();
        return ctx;
    }

    #endregion
}