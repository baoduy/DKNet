using DKNet.EfCore.Extensions.Configurations;

namespace EfCore.Extensions.Tests;

// Test seeding configuration for testing
public class UserSeedingConfiguration : IDataSeedingConfiguration<User>
{
    public ICollection<User> Data => new List<User>
    {
        new(1, "seeded1")
        {
            Account = new Account { UserName = "Steven", Password = "Pass@word1",},
            FirstName = "Seeded", LastName = "User1"
        },
        new(2, "seeded2")
        {
            Account = new Account { UserName = "Steven", Password = "Pass@word1" }, FirstName = "Seeded",
            LastName = "User2"
        }
    };
}


public class DataSeedingTests : SqlServerTestBase
{
    [Fact]
    public async Task UseAutoDataSeeding_ShouldSeedDataFromConfigurations()
    {
        // Arrange
        await StartSqlContainerAsync();

        var options = new DbContextOptionsBuilder<MyDbContext>()
            .UseSqlServer(GetConnectionString("SeedingDb"))
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

    // [Fact]
    // public async Task UseAutoDataSeeding_WithExistingData_ShouldNotDuplicateData()
    // {
    //     // Arrange
    //     var container = await StartSqlContainerAsync();
    //     var connectionString = container.GetConnectionString();
    //
    //     var options = new DbContextOptionsBuilder<MyDbContext>()
    //         .UseSqlServer(connectionString)
    //         .UseAutoConfigModel(op => op.ScanFrom(typeof(MyDbContext).Assembly))
    //         .UseAutoDataSeeding(typeof(UserSeedingConfiguration).Assembly)
    //         .Options;
    //
    //     await using var context = new MyDbContext(options);
    //     await context.Database.EnsureCreatedAsync();
    //
    //     // Manually add one of the seeded entities first
    //     context.Set<User>().Add(new User(1, "creator")
    //     {
    //         FirstName = "Already",
    //         LastName = "Exists"
    //     });
    //     await context.SaveChangesAsync();
    //
    //     // Clear change tracker
    //     context.ChangeTracker.Clear();
    //
    //     // Act - Try to seed again
    //     await context.SaveChangesAsync();
    //
    //     // Assert - Should not duplicate the existing user
    //     var users = await context.Set<User>().Where(u => u.Id == 1).ToListAsync();
    //     users.Count.ShouldBe(1);
    //     users.First().FirstName.ShouldBe("Already"); // Original data should remain
    // }

    [Fact]
    public void UseAutoDataSeeding_WithNullOptionsBuilder_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            ((DbContextOptionsBuilder)null!).UseAutoDataSeeding(typeof(UserSeedingConfiguration).Assembly));
    }
}