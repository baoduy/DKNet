using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;

namespace EfCore.Repos.Tests;

/// <summary>
///     Tests for the IRepositorySpec and RepositorySpec implementation
/// </summary>
public class RepositorySpecTests : IAsyncLifetime
{
    #region Fields

    private SqliteConnection? _connection;
    private TestDbContext _context = null!;
    private IMapper _mapper = null!;
    private IRepositorySpec _repository = null!;

    #endregion

    #region Methods

    [Fact]
    public async Task AddAsync_WithValidEntity_ShouldAddToDatabase()
    {
        // Arrange
        var user = new User("test") { FirstName = "New", LastName = "User" };

        // Act
        await this._repository.AddAsync(user);
        await this._repository.SaveChangesAsync();

        // Assert
        var result = await this._context.Users.FindAsync(user.Id);
        result.ShouldNotBeNull();
        result.FirstName.ShouldBe("New");
        result.LastName.ShouldBe("User");
    }

    [Fact]
    public async Task AddRangeAsync_WithMultipleEntities_ShouldAddAllToDatabase()
    {
        // Arrange
        var users = new[]
        {
            new User("test") { FirstName = "User1", LastName = "Test1" },
            new User("test") { FirstName = "User2", LastName = "Test2" },
            new User("test") { FirstName = "User3", LastName = "Test3" }
        };

        // Act
        await this._repository.AddRangeAsync(users);
        await this._repository.SaveChangesAsync();

        // Assert
        var result = await this._context.Users.ToListAsync();
        result.Count.ShouldBe(3);
        result.ShouldContain(u => u.FirstName == "User1");
        result.ShouldContain(u => u.FirstName == "User2");
        result.ShouldContain(u => u.FirstName == "User3");
    }

    [Fact]
    public async Task BeginTransactionAsync_ShouldCreateTransaction()
    {
        // Act
        var transaction = await this._repository.BeginTransactionAsync();

        // Assert
        transaction.ShouldNotBeNull();
        await transaction.DisposeAsync();
    }

    [Fact]
    public async Task Delete_WithEntity_ShouldRemoveFromDatabase()
    {
        // Arrange
        var user = new User("test") { FirstName = "ToDelete", LastName = "User" };
        await this._repository.AddAsync(user);
        await this._repository.SaveChangesAsync();

        // Act
        this._repository.Delete(user);
        await this._repository.SaveChangesAsync();

        // Assert
        var result = await this._context.Users.FindAsync(user.Id);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteRange_WithMultipleEntities_ShouldRemoveAllFromDatabase()
    {
        // Arrange
        var users = new[]
        {
            new User("test") { FirstName = "Delete1", LastName = "Test1" },
            new User("test") { FirstName = "Delete2", LastName = "Test2" }
        };
        await this._repository.AddRangeAsync(users);
        await this._repository.SaveChangesAsync();

        // Act
        this._repository.DeleteRange(users);
        await this._repository.SaveChangesAsync();

        // Assert
        var result = await this._context.Users.ToListAsync();
        result.ShouldBeEmpty();
    }

    public async Task DisposeAsync()
    {
        //await _context.Database.EnsureDeletedAsync();
        await this._context.DisposeAsync();
        if (this._connection != null)
        {
            await this._connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Entry_ModifyingProperties_ShouldTrackChanges()
    {
        // Arrange
        var user = new User("test") { FirstName = "Original", LastName = "Name" };
        await this._repository.AddAsync(user);
        await this._repository.SaveChangesAsync();

        // Act
        user.FirstName = "Modified";
        var entry = this._repository.Entry(user);

        // Assert
        entry.State.ShouldBe(EntityState.Modified);
        entry.Property(u => u.FirstName).IsModified.ShouldBeTrue();
    }

    [Fact]
    public async Task Entry_WithEntity_ShouldReturnEntityEntry()
    {
        // Arrange
        var user = new User("test") { FirstName = "Entry", LastName = "Test" };
        await this._repository.AddAsync(user);

        // Act
        var entry = this._repository.Entry(user);

        // Assert
        entry.ShouldNotBeNull();
        entry.Entity.ShouldBe(user);
        entry.State.ShouldBe(EntityState.Added);
    }

    public async Task InitializeAsync()
    {
        // Use a shared connection for SQLite in-memory database
        this._connection = new SqliteConnection("DataSource=:memory:");
        await this._connection.OpenAsync();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(this._connection)
            .Options;

        this._context = new TestDbContext(options);
        await this._context.Database.EnsureCreatedAsync();

        // Setup Mapster
        var config = new TypeAdapterConfig();
        config.NewConfig<User, UserDto>()
            .Map(dest => dest.FullName, src => $"{src.FirstName} {src.LastName}");
        this._mapper = new Mapper(config);

        this._repository = new RepositorySpec<TestDbContext>(this._context, [this._mapper]);
    }

    [Fact]
    public async Task Query_WithOrdering_ShouldReturnOrderedResults()
    {
        // Arrange
        await this.SeedTestUsers();
        var spec = new OrderedUsersSpecification();

        // Act
        var result = await this._repository.Query(spec).ToListAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBeGreaterThan(0);

        // Verify ordering by LastName
        for (var i = 0; i < result.Count - 1; i++)
        {
            string.Compare(result[i].LastName, result[i + 1].LastName, StringComparison.Ordinal)
                .ShouldBeLessThanOrEqualTo(0);
        }
    }

    [Fact]
    public void Query_WithProjection_NoMapperAvailable_ShouldThrowException()
    {
        // Arrange
        var repositoryNoMapper = new RepositorySpec<TestDbContext>(this._context, []);
        var spec = new ActiveUsersSpecification();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
        {
            var query = repositoryNoMapper.Query<User, UserDto>(spec);
            _ = query.ToList(); // Force query execution
        });
    }

    [Fact]
    public async Task Query_WithProjection_ShouldReturnMappedModels()
    {
        // Arrange
        await this.SeedTestUsers();
        var spec = new ActiveUsersSpecification();

        // Act
        var result = await this._repository.Query<User, UserDto>(spec).ToListAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldAllBe(dto => !string.IsNullOrEmpty(dto.FullName));
        result.First().FullName.ShouldContain("Active");
    }

    [Fact]
    public async Task Query_WithSpecification_ShouldReturnFilteredEntities()
    {
        // Arrange
        await this.SeedTestUsers();
        var spec = new ActiveUsersSpecification();

        // Act
        var result = await this._repository.Query(spec).ToListAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldAllBe(u => u.FirstName.StartsWith("Active"));
    }

    [Fact]
    public async Task SaveChangesAsync_WithCancellation_ShouldRespectToken()
    {
        // Arrange
        var user = new User("test") { FirstName = "Cancel", LastName = "Test" };
        await this._repository.AddAsync(user);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await this._repository.SaveChangesAsync(cts.Token);
        });
    }

    [Fact]
    public async Task SaveChangesAsync_WithMultipleOperations_ShouldPersistAll()
    {
        // Arrange
        var user1 = new User("test") { FirstName = "Add", LastName = "User" };
        var user2 = new User("test") { FirstName = "Delete", LastName = "User" };
        await this._repository.AddAsync(user2);
        await this._repository.SaveChangesAsync();

        // Act
        await this._repository.AddAsync(user1);
        this._repository.Delete(user2);
        var count = await this._repository.SaveChangesAsync();

        // Assert
        count.ShouldBeGreaterThan(0);
        var results = await this._context.Users.ToListAsync();
        results.Count.ShouldBe(1);
        results[0].FirstName.ShouldBe("Add");
    }

    private async Task SeedTestUsers()
    {
        var users = new[]
        {
            new User("test") { FirstName = "ActiveOne", LastName = "Smith" },
            new User("test") { FirstName = "ActiveTwo", LastName = "Anderson" },
            new User("test") { FirstName = "Inactive", LastName = "Johnson" }
        };

        await this._context.Users.AddRangeAsync(users);
        await this._context.SaveChangesAsync();
    }

    [Fact]
    public async Task Transaction_WithCommit_ShouldPersistChanges()
    {
        // Arrange
        var user = new User("test") { FirstName = "Transaction", LastName = "User" };

        // Act
        using var transaction = await this._repository.BeginTransactionAsync();
        await this._repository.AddAsync(user);
        await this._repository.SaveChangesAsync();
        await transaction.CommitAsync();

        // Assert
        var result = await this._context.Users.FindAsync(user.Id);
        result.ShouldNotBeNull();
        result.FirstName.ShouldBe("Transaction");
    }

    [Fact]
    public async Task Transaction_WithRollback_ShouldNotPersistChanges()
    {
        // Arrange
        var user = new User("test") { FirstName = "Rollback", LastName = "User" };

        // Act
        using var transaction = await this._repository.BeginTransactionAsync();
        await this._repository.AddAsync(user);
        await this._repository.SaveChangesAsync();
        await transaction.RollbackAsync();

        // Clear the context to get fresh data
        this._context.ChangeTracker.Clear();

        // Assert
        var result = await this._context.Users.ToListAsync();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_WithModifiedEntity_ShouldUpdateInDatabase()
    {
        // Arrange
        var user = new User("test") { FirstName = "Original", LastName = "Name" };
        await this._repository.AddAsync(user);
        await this._repository.SaveChangesAsync();

        // Detach to simulate getting entity from another context
        this._context.Entry(user).State = EntityState.Detached;

        // Act
        user.FirstName = "Updated";
        await this._repository.UpdateAsync(user);
        await this._repository.SaveChangesAsync();

        // Assert
        var result = await this._context.Users.FindAsync(user.Id);
        result.ShouldNotBeNull();
        result.FirstName.ShouldBe("Updated");
    }

    [Fact]
    public async Task UpdateRangeAsync_WithMultipleEntities_ShouldUpdateAll()
    {
        // Arrange
        var users = new[]
        {
            new User("test") { FirstName = "User1", LastName = "Test1" },
            new User("test") { FirstName = "User2", LastName = "Test2" }
        };
        await this._repository.AddRangeAsync(users);
        await this._repository.SaveChangesAsync();

        // Detach entities
        foreach (var user in users)
        {
            this._context.Entry(user).State = EntityState.Detached;
        }

        // Act
        users[0].FirstName = "UpdatedUser1";
        users[1].FirstName = "UpdatedUser2";
        await this._repository.UpdateRangeAsync(users);
        await this._repository.SaveChangesAsync();

        // Assert
        var results = await this._context.Users.ToListAsync();
        results.ShouldContain(u => u.FirstName == "UpdatedUser1");
        results.ShouldContain(u => u.FirstName == "UpdatedUser2");
    }

    #endregion

    private class ActiveUsersSpecification : Specification<User>
    {
        #region Constructors

        public ActiveUsersSpecification()
        {
            this.WithFilter(u => u.FirstName.StartsWith("Active"));
            this.AddOrderBy(u => u.LastName);
        }

        #endregion
    }

    private class OrderedUsersSpecification : Specification<User>
    {
        #region Constructors

        public OrderedUsersSpecification()
        {
            this.AddOrderBy(u => u.LastName);
            this.AddOrderBy(u => u.FirstName);
        }

        #endregion
    }

    public class UserDto
    {
        #region Properties

        public int Id { get; set; }

        public string FullName { get; set; } = string.Empty;

        #endregion
    }
}