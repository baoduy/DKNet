using DKNet.EfCore.Extensions.Configurations;

namespace EfCore.Extensions.Tests;

// Test seeding configuration for testing
public class UserSeedingConfiguration : IDataSeedingConfiguration<User>
{
    public ICollection<User> Data =>
    [
        new("seeded1")
        {
            Account = new Account { UserName = "Steven", Password = "Pass@word1" },
            FirstName = "Seeded", LastName = "User1"
        },
        new("seeded2")
        {
            Account = new Account { UserName = "Steven", Password = "Pass@word1" }, FirstName = "Seeded",
            LastName = "User2"
        }
    ];
}

public class DataSeedingTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task UseAutoDataSeeding_ShouldSeedDataFromConfigurations()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MyDbContext>()
            .UseSqlServer(fixture.GetConnectionString("SeedingDb"))
            .UseAutoConfigModel(op => op.ScanFrom(typeof(MyDbContext).Assembly))
            .UseAutoDataSeeding(typeof(UserSeedingConfiguration).Assembly)
            .Options;

        await using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        // Act - The seeding should happen automatically
        await context.SaveChangesAsync();

        // Assert
        var users = await context.Set<User>().ToListAsync();
        users.ShouldContain(u => u.FirstName == "Seeded" && u.LastName == "User1");
        users.ShouldContain(u => u.FirstName == "Seeded" && u.LastName == "User2");
    }

    [Fact]
    public void UseAutoDataSeeding_WithNullOptionsBuilder_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            ((DbContextOptionsBuilder)null!).UseAutoDataSeeding(typeof(UserSeedingConfiguration).Assembly));
    }
}