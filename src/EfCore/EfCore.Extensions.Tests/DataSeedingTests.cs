using DKNet.EfCore.Extensions.Configurations;

namespace EfCore.Extensions.Tests;

// Test seeding configuration for testing
public class UserSeedingConfiguration : DataSeedingConfiguration<User>
{
    protected override ICollection<User> HasData =>
    [
        new(1, "seeded1")
        {
            FirstName = "Seeded", LastName = "User1"
        },
        new(2, "seeded2")
        {
            FirstName = "Seeded",
            LastName = "User2"
        }
    ];
}

public class DataSeedingTests
{
    [Fact]
    public async Task UseAutoDataSeeding_ShouldSeedDataFromConfigurations()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase("TestDb_Seeding")
            .UseAutoConfigModel()
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