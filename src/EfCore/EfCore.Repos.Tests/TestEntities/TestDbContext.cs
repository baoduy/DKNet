using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.Repos.Tests.TestEntities;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    #region Properties

    public DbSet<Address> Addresses { get; set; }
    public DbContextOptions<TestDbContext> Options => options;

    public DbSet<User> Users { get; set; }

    #endregion

    #region Methods

    public override int SaveChanges()
    {
        UpdateConcurrencyTokens();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateConcurrencyTokens();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateConcurrencyTokens()
    {
        // For SQLite, manually update RowVersion for modified entities
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified || e.State == EntityState.Added);

        foreach (var entry in entries)
        {
            if (entry.Entity is IConcurrencyEntity<byte[]> concurrencyEntity)
            {
                // Generate a new version using a timestamp
                concurrencyEntity.SetRowVersion(BitConverter.GetBytes(DateTime.UtcNow.Ticks));
            }
        }
    }

    #endregion
}