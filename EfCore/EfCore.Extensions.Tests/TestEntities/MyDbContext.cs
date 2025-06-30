using System.Diagnostics.CodeAnalysis;

namespace EfCore.Extensions.Tests.TestEntities;

[ExcludeFromCodeCoverage]
public class MyDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Account> Accounts { get; set; }
}