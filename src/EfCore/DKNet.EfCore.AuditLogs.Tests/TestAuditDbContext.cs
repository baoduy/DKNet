using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.AuditLogs.Tests;

// Test entity implementing audited functionality with additional diverse properties
public sealed class TestAuditEntity() : AuditedEntity<Guid>(Guid.NewGuid())
{
    [MaxLength(100)] public required string Name { get; set; }
    public int Age { get; set; }
    public bool IsActive { get; set; }
    public decimal Balance { get; set; }
    public DateTimeOffset? LastLoginOn { get; set; }

    [MaxLength(200)] public string? Notes { get; set; }

    // Helper to simulate an update cycle in tests
    public void UpdateProfile(string updater, string? notes = null, DateTimeOffset? updatedOn = null)
    {
        Notes = notes ?? Notes;
        SetUpdatedBy(updater, updatedOn);
    }
}

// DbContext for testing audit logging
public sealed class TestAuditDbContext(DbContextOptions<TestAuditDbContext> options) : DbContext(options)
{
    public DbSet<TestAuditEntity> AuditEntities => Set<TestAuditEntity>();
    public DbSet<PlainEntity> PlainEntities => Set<PlainEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var e = modelBuilder.Entity<TestAuditEntity>();
        e.Property(p => p.Name).IsRequired().HasMaxLength(200);
        e.Property(p => p.Notes).HasMaxLength(1000);
        e.Property(p => p.Balance).HasPrecision(18, 2);

        // Minimal configuration for PlainEntity (optional as conventions handle it)
        modelBuilder.Entity<PlainEntity>().ToTable("PlainEntities");
    }
}