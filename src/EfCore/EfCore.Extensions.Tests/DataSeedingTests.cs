using DKNet.EfCore.Extensions.Configurations;
using DKNet.EfCore.Extensions.Extensions;

namespace EfCore.Extensions.Tests;

// Test seeding configuration for testing
public class UserSeedingConfiguration : DataSeedingConfiguration<User>
{
    #region Methods

    protected override ValueTask<ICollection<User>> GetDataAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<ICollection<User>>(
        [
            new User(
                1, "seeded1")
            {
                FirstName = "Seeded", LastName = "User1"
            },
            new User(2, "seeded2")
            {
                FirstName = "Seeded",
                LastName = "User2"
            }
        ]);

    #endregion
}

public class DataSeedingTests
{
    #region Methods

    [Fact]
    public async Task UseAutoDataSeeding_ShouldSeedDataFromConfigurations()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase("TestDb_Seeding")
            .UseAutoConfigModel()
            .UseAutoDataSeeding([typeof(UserSeedingConfiguration).Assembly])
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
            ((DbContextOptionsBuilder)null!).UseAutoDataSeeding([typeof(UserSeedingConfiguration).Assembly]));
    }

    [Fact]
    public async Task SeedAsync_RunTwice_DoesNotInsertDuplicatesForReferenceEqualityEntity()
    {
        // User has no Equals/GetHashCode override, so comparing seed candidates against existing rows by
        // reference equality (the pre-fix behavior) would never match and would re-insert both rows on every
        // run. This proves the fix - which compares by primary key instead - makes the second run a no-op.
        var options = new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase("DataSeeding_TwiceRun_" + Guid.NewGuid())
            .Options;

        await using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var config = new UserSeedingConfiguration();

        // Act - invoke the seed callback twice against the same underlying store.
        await config.SeedAsync!(context, false, CancellationToken.None);
        await config.SeedAsync!(context, false, CancellationToken.None);

        // Assert - still only the two seeded rows, not four.
        var users = await context.Set<User>().Where(u => u.FirstName == "Seeded").ToListAsync();
        users.Count.ShouldBe(2);
    }

    #endregion
}