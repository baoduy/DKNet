// <copyright file="Merchant.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace EfCore.Specifications.Tests.TestEntities;

/// <summary>
///     A minimal entity used only to exercise three-key keyset pagination
///     (country ascending, revenue descending, identifier ascending) against real SQL Server.
/// </summary>
public class Merchant
{
    public int Id { get; set; }
    public string Country { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
///     A dedicated, minimal <see cref="DbContext" /> for the three-key keyset pagination integration
///     scenario, kept separate from <see cref="TestDbContext" /> so it can target real SQL Server via
///     TestContainers without pulling in the rest of the specifications test model.
/// </summary>
public class MerchantDbContext(DbContextOptions<MerchantDbContext> options) : DbContext(options)
{
    public DbSet<Merchant> Merchants => Set<Merchant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Merchant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Revenue).HasPrecision(18, 2);
        });
    }
}
