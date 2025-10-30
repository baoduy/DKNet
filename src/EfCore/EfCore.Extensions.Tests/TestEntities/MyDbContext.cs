using System.Diagnostics.CodeAnalysis;

namespace EfCore.Extensions.Tests.TestEntities;

[ExcludeFromCodeCoverage]
public class MyDbContext(DbContextOptions options) : DbContext(options)
{
    #region Properties

    public DbSet<Account> Accounts { get; set; }

    public DbSet<User> Users { get; set; }

    #endregion
}