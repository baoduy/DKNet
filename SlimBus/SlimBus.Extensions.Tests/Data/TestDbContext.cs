namespace SlimBus.Extensions.Tests.Data;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public static bool Called { get; set; }

    public virtual DbSet<TestEntity> Entities { get; set; } = null!;


    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = new())
    {
        Called = true;
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}