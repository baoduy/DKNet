using Microsoft.EntityFrameworkCore;

namespace EfCore.Repos.Tests.TestEntities;

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
}