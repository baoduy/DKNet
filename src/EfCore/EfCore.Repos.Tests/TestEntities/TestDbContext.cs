namespace EfCore.Repos.Tests.TestEntities;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    #region Properties

    public DbSet<Address> Addresses { get; set; }
    public DbContextOptions<TestDbContext> Options => options;

    public DbSet<User> Users { get; set; }

    #endregion
}