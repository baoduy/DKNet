namespace EfCore.Extensions.Tests;

/// <summary>
/// Concurrent update needs to be tested with real SQL Server.
/// </summary>
public class ConcurrentUpdateWithSqlTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task ConcurrencyWithRepositoryTest()
    {
        var writeRepo = new WriteRepository<User>(fixture.Db);
        var readRepo = new ReadRepository<User>(fixture.Db);
        //1. Create a new User.
        var user = new User("A")
        {
            FirstName = "Duy",
            LastName = "Hoang",
            Addresses =
            {
                new Address
                {
                    OwnedEntity = new OwnedEntity { Name = "123" },
                    City = "HBD",
                    Street = "HBD"
                },
                new Address
                {
                    OwnedEntity = new OwnedEntity { Name = "123" },
                    City = "HBD",
                    Street = "HBD"
                }
            },
        };

        await writeRepo.AddAsync(user);
        await writeRepo.SaveChangesAsync();

        var createdVersion = (byte[])user.RowVersion!.Clone();

        //2. Update user with a created version. It should allow to update.
        // Change the person's name in the database to simulate a concurrency conflict.
        user.FirstName = "Duy3";
        user.SetUpdatedBy("System");
        await writeRepo.SaveChangesAsync();

        //3. Update user with a created version again. It should NOT allow to update.
        user = await readRepo.FindAsync(user.Id);
        user!.FirstName = "Duy3";
        user.SetRowVersion(createdVersion);

        //The DbUpdateConcurrencyException will be thrown here
        var fun = async () =>
        {
            await writeRepo.UpdateAsync(user);
            await writeRepo.SaveChangesAsync();
        };

        await fun.ShouldThrowAsync<DbUpdateConcurrencyException>();
    }
}