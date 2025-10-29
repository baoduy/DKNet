using System.Data;

namespace EfCore.Extensions.Tests;

#pragma warning disable CA2012 // Use ValueTasks correctly

public class WithSqlDbTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    #region Fields

    private readonly MyDbContext _db = fixture.Db!;

    #endregion

    #region Methods

    [Fact]
    public async Task DatabaseConnection_ShouldOpenAndClose()
    {
        // Act
        await this._db.Database.OpenConnectionAsync();
        var connectionState = this._db.Database.GetDbConnection().State;

        // Assert
        connectionState.ShouldBe(ConnectionState.Open);

        // Cleanup
        await this._db.Database.CloseConnectionAsync();
        connectionState = this._db.Database.GetDbConnection().State;
        connectionState.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public void DatabaseProvider_ShouldBeSqlServer()
    {
        // Act
        var providerName = this._db.Database.ProviderName;

        // Assert
        providerName.ShouldBe("Microsoft.EntityFrameworkCore.SqlServer");
    }

    [Fact]
    public async Task NextSeqValueWithFormat_WithEmptyFormatString_ShouldReturnValueAsString()
    {
        // This test verifies the format processing logic
        var result = await this._db.NextSeqValueWithFormat(TestSequenceTypes.TestSequence1);
        result.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task SequenceValueTestAsync()
    {
        var val1 = await this._db.NextSeqValue<SequencesTest, short>(SequencesTest.Invoice);
        val1!.Value.ShouldBeGreaterThan((short)0);

        var val2 = await this._db.NextSeqValue<SequencesTest, int>(SequencesTest.Order);
        val2!.Value.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SequenceValueWithFormatTestAsync()
    {
        var val1 = await this._db.NextSeqValueWithFormat(SequencesTest.Invoice);
        val1.ShouldContain(string.Format(CultureInfo.CurrentCulture, "T{0:yyMMdd}0000", DateTime.Now));
    }

    [Fact]
    public async Task TestConcurrentUpdateThrowsExceptionAsync()
    {
        // Arrange
        var user = new User("ConcurrencyTest")
        {
            FirstName = "Test",
            LastName = "User"
        };
        this._db.Set<User>().Add(user);
        await this._db.SaveChangesAsync();

        // Create new contexts with same configuration
        var dbOptions = this._db.GetService<IDbContextServices>().ContextOptions;

        await using var db1 = new MyDbContext(dbOptions);
        await using var db2 = new MyDbContext(dbOptions);

        var user1 = await db1.Set<User>().FindAsync(user.Id);
        var user2 = await db2.Set<User>().FindAsync(user.Id);

        // Act - First update
        user1!.FirstName = "Updated1";
        await db1.SaveChangesAsync();

        // Second update should conflict
        user2!.FirstName = "Updated2";
        Func<Task> act = async () => await db2.SaveChangesAsync();

        // Assert
        await act.ShouldThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task TestCreateWithSqlDbAsync()
    {
        this._db.ChangeTracker.Clear();

        //Create User with Address
        this._db.Set<User>().Add(
            new User("Duy")
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
                    }
                }
            });

        var count = await this._db.SaveChangesAsync();
        (count >= 1).ShouldBeTrue();

        var users = await this._db.Set<User>().ToListAsync();

        (users.Count >= 1).ShouldBeTrue();
        users.TrueForAll(u => u.RowVersion != null).ShouldBeTrue();
    }

    [Fact]
    public async Task TestDeleteWithSqlDbAsync()
    {
        await this.TestCreateWithSqlDbAsync();

        var user = await this._db.Set<User>().Include(u => u.Addresses)
            .OrderByDescending(c => c.CreatedOn).FirstAsync();

        this._db.RemoveRange(user.Addresses);
        this._db.Remove(user);

        await this._db.SaveChangesAsync();

        var count = await this._db.Set<User>().CountAsync(u => u.Id == user.Id);

        (count == 0).ShouldBeTrue();
    }

    [Fact]
    public async Task TestUpdateWithSqlDbAsync()
    {
        await this.TestCreateWithSqlDbAsync();

        var user = await this._db.Set<User>().Include(u => u.Addresses).OrderByDescending(u => u.CreatedOn)
            .FirstAsync();

        user.FirstName = "Steven";
        user.Addresses.Last().Street = "Steven Street";

        await this._db.SaveChangesAsync();

        user = await this._db.Set<User>().Include(i => i.Addresses).OrderByDescending(u => u.CreatedOn).FirstAsync();

        string.Equals(user.FirstName, "Steven", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();

        string.Equals(user.Addresses.Last().Street, "Steven Street", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    #endregion
}