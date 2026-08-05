using System.Diagnostics.CodeAnalysis;
using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.Extensions.Tests.TestEntities;

[ExcludeFromCodeCoverage]
public class MyDbContext(DbContextOptions options) : DbContext(options)
{
    #region Properties

    public DbSet<Account> Accounts { get; set; }

    public DbSet<User> Users { get; set; }

    #endregion

    #region Methods

    // Postgres has no native rowversion column type, so the concurrency token is stamped here instead
    // of relying on server-side auto-generation (see BaseEntityMapper.UserEntityConfig).
    private void StampRowVersions()
    {
        foreach (var entry in ChangeTracker.Entries<IConcurrencyEntity<byte[]>>())
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.SetRowVersion(Guid.NewGuid().ToByteArray());
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    #endregion
}