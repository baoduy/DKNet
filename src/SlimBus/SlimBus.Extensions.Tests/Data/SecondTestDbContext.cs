namespace SlimBus.Extensions.Tests.Data;

// A second, independent DbContext type: the registry-isolation and duplicate-registration
// scenarios need a distinct TDbContext from TestDbContext to prove one provider's auto-save
// registry cannot see or be confused with another provider's.
public class SecondTestDbContext(DbContextOptions<SecondTestDbContext> options) : DbContext(options)
{
    #region Properties

    public int SaveCount { get; private set; }

    public virtual DbSet<SecondTestEntity> Entities { get; set; } = null!;

    #endregion

    #region Methods

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = new())
    {
        SaveCount++;
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    #endregion
}
