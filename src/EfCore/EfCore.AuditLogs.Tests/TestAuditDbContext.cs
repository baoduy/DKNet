using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace EfCore.AuditLogs.Tests;

// Test entity implementing audited functionality with additional diverse properties
public sealed class TestAuditEntity() : AuditedEntity<Guid>(Guid.NewGuid())
{
    #region Properties

    public bool IsActive { get; set; }

    public DateTimeOffset? LastLoginOn { get; set; }

    public decimal Balance { get; set; }

    public int Age { get; set; }

    [MaxLength(100)] public required string Name { get; set; }

    [MaxLength(200)] public string? Notes { get; set; }

    #endregion

    #region Methods

    // Helper to simulate an update cycle in tests
    public void UpdateProfile(string updater, string? notes = null, DateTimeOffset? updatedOn = null)
    {
        this.Notes = notes ?? this.Notes;
        this.SetUpdatedBy(updater, updatedOn);
    }

    #endregion
}

// DbContext for testing audit logging
public sealed class TestAuditDbContext(DbContextOptions<TestAuditDbContext> options) : DbContext(options)
{
    #region Properties

    public DbSet<PlainEntity> PlainEntities => this.Set<PlainEntity>();

    public DbSet<TestAuditEntity> AuditEntities => this.Set<TestAuditEntity>();

    #endregion

    #region Methods

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

    #endregion
}