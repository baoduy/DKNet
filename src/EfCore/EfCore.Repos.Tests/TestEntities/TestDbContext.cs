using Microsoft.EntityFrameworkCore;

namespace EfCore.Repos.Tests.TestEntities;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbContextOptions<TestDbContext> Options => options;
    public DbSet<User> Users { get; set; }
}