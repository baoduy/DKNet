using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;

namespace EfCore.Repos.Tests;

/// <summary>
///     Tests for the SpecRepoExtensions methods
/// </summary>
public class SpecRepoExtensionsTests : IAsyncLifetime
{
    #region Fields

    private SqliteConnection? _connection;
    private TestDbContext _context = null!;
    private IMapper _mapper = null!;
    private IRepositorySpec _repository = null!;

    #endregion

    #region Methods

    [Fact]
    public async Task AnyAsync_WithMatchingEntities_ShouldReturnTrue()
    {
        // Arrange
        await this.SeedTestUsers();
        var spec = new ActiveUsersSpecification();

        // Act
        var result = await this._repository.AnyAsync(spec);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task AnyAsync_WithNoMatches_ShouldReturnFalse()
    {
        // Arrange
        var spec = new ActiveUsersSpecification();

        // Act
        var result = await this._repository.AnyAsync(spec);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task AnyAsync_WithSpecificCondition_ShouldReturnCorrectResult()
    {
        // Arrange
        await this.SeedTestUsers();
        var spec = new UserByFirstNameSpecification("Inactive");

        // Act
        var result = await this._repository.AnyAsync(spec);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task CountAsync_WithAllEntities_ShouldReturnTotalCount()
    {
        // Arrange
        await this.SeedTestUsers();
        var spec = new AllUsersSpecification();

        // Act
        var result = await this._repository.CountAsync(spec);

        // Assert
        result.ShouldBe(3);
    }

    [Fact]
    public async Task CountAsync_WithMatchingEntities_ShouldReturnCount()
    {
        // Arrange
        await this.SeedTestUsers();
        var spec = new ActiveUsersSpecification();

        // Act
        var result = await this._repository.CountAsync(spec);

        // Assert
        result.ShouldBe(2);
    }

    [Fact]
    public async Task CountAsync_WithNoMatches_ShouldReturnZero()
    {
        // Arrange
        var spec = new ActiveUsersSpecification();

        // Act
        var result = await this._repository.CountAsync(spec);

        // Assert
        result.ShouldBe(0);
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
    public async Task FirstAsync_WithMatchingEntity_ShouldReturnEntity()
    {
        // Arrange
        await this.SeedTestUsers();
        var spec = new ActiveUsersSpecification();

        // Act
        var result = await this._repository.FirstAsync(spec);

        // Assert
        result.ShouldNotBeNull();
        result.FirstName.ShouldStartWith("Active");
    }

    [Fact]
    public async Task FirstAsync_WithNoMatch_ShouldThrowException()
    {
        // Arrange
        var spec = new ActiveUsersSpecification();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () => { await this._repository.FirstAsync(spec); });
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithMatchingEntity_ShouldReturnEntity()
    {
        // Arrange
        await this.SeedTestUsers();
        var spec = new UserByFirstNameSpecification("ActiveOne");

        // Act
        var result = await this._repository.FirstOrDefaultAsync(spec);

        // Assert
        result.ShouldNotBeNull();
        result.FirstName.ShouldBe("ActiveOne");
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithNoMatch_ShouldReturnNull()
    {
        // Arrange
        await this.SeedTestUsers();
        var spec = new UserByFirstNameSpecification("NonExistent");

        // Act
        var result = await this._repository.FirstOrDefaultAsync(spec);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithProjection_ShouldReturnProjectedModel()
    {
        // Arrange
        await this.SeedTestUsers();
        var spec = new UserByFirstNameSpecification("ActiveOne");

        // Act
        var result = await this._repository.FirstOrDefaultAsync<User, UserDto>(spec);

        // Assert
        result.ShouldNotBeNull();
        result.FullName.ShouldContain("ActiveOne");
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithProjectionNoMatch_ShouldReturnNull()
    {
        // Arrange
        await this.SeedTestUsers();
        var spec = new UserByFirstNameSpecification("NonExistent");

        // Act
        var result = await this._repository.FirstOrDefaultAsync<User, UserDto>(spec);

        // Assert
        result.ShouldBeNull();
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
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.FullName, src => $"{src.FirstName} {src.LastName}");
        this._mapper = new Mapper(config);

        this._repository = new RepositorySpec<TestDbContext>(this._context, [this._mapper]);
    }

    private async Task SeedManyUsers(int count)
    {
        var users = new List<User>();
        for (var i = 1; i <= count; i++)
        {
            var prefix = i % 2 == 0 ? "Active" : "Inactive";
            users.Add(
                new User("test")
                {
                    FirstName = $"{prefix}User{i}",
                    LastName = $"LastName{i}"
                });
        }

        await this._context.Users.AddRangeAsync(users);
        await this._context.SaveChangesAsync();
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
    public async Task ToListAsync_WithCancellation_ShouldRespectToken()
    {
        // Arrange
        await this.SeedTestUsers();
        var spec = new ActiveUsersSpecification();
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await this._repository.ToListAsync(spec, cts.Token);
        });
    }

    [Fact]
    public async Task ToListAsync_WithEmptyResult_ShouldReturnEmptyList()
    {
        // Arrange
        var spec = new ActiveUsersSpecification();

        // Act
        var result = await this._repository.ToListAsync(spec);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ToListAsync_WithProjection_ShouldReturnListOfModels()
    {
        // Arrange
        await this.SeedTestUsers();
        var spec = new ActiveUsersSpecification();

        // Act
        var result = await this._repository.ToListAsync<User, UserDto>(spec);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldAllBe(dto => !string.IsNullOrEmpty(dto.FullName));
        result.First().FullName.ShouldContain("Active");
    }

    [Fact]
    public async Task ToListAsync_WithSpecification_ShouldReturnListOfEntities()
    {
        // Arrange
        await this.SeedTestUsers();
        var spec = new ActiveUsersSpecification();

        // Act
        var result = await this._repository.ToListAsync(spec);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldAllBe(u => u.FirstName.StartsWith("Active"));
    }

    [Fact]
    public async Task ToPagedListAsync_WithEmptyResult_ShouldReturnEmptyPagedList()
    {
        // Arrange
        var spec = new AllUsersSpecification();

        // Act
        var result = await this._repository.ToPagedListAsync(spec, 1, 10);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
        result.PageCount.ShouldBe(0);
        result.TotalItemCount.ShouldBe(0);
    }

    [Fact]
    public async Task ToPagedListAsync_WithEntities_ShouldReturnPagedList()
    {
        // Arrange
        await this.SeedManyUsers(15);
        var spec = new AllUsersSpecification();

        // Act
        var result = await this._repository.ToPagedListAsync(spec, 1, 5);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(5);
        result.PageCount.ShouldBe(3);
        result.PageNumber.ShouldBe(1);
        result.PageSize.ShouldBe(5);
        result.TotalItemCount.ShouldBe(15);
        result.HasNextPage.ShouldBeTrue();
        result.HasPreviousPage.ShouldBeFalse();
    }

    [Fact]
    public async Task ToPagedListAsync_WithFilteredSpec_ShouldReturnFilteredPagedList()
    {
        // Arrange
        await this.SeedManyUsers(20);
        var spec = new ActiveUsersSpecification();

        // Act
        var result = await this._repository.ToPagedListAsync(spec, 1, 5);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(5);
        result.ShouldAllBe(u => u.FirstName.StartsWith("Active"));
    }

    [Fact]
    public async Task ToPagedListAsync_WithLastPage_ShouldReturnPartialPage()
    {
        // Arrange
        await this.SeedManyUsers(15);
        var spec = new AllUsersSpecification();

        // Act
        var result = await this._repository.ToPagedListAsync(spec, 3, 5);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(5);
        result.PageNumber.ShouldBe(3);
        result.HasNextPage.ShouldBeFalse();
        result.HasPreviousPage.ShouldBeTrue();
        result.IsLastPage.ShouldBeTrue();
    }

    [Fact]
    public async Task ToPagedListAsync_WithProjection_ShouldReturnPagedListOfModels()
    {
        // Arrange
        await this.SeedManyUsers(10);
        var spec = new AllUsersSpecification();

        // Act
        var result = await this._repository.ToPagedListAsync<User, UserDto>(spec, 1, 5);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(5);
        result.PageCount.ShouldBe(2);
        result.TotalItemCount.ShouldBe(10);
        result.ShouldAllBe(dto => !string.IsNullOrEmpty(dto.FullName));
    }

    [Fact]
    public async Task ToPagedListAsync_WithSecondPage_ShouldReturnCorrectPage()
    {
        // Arrange
        await this.SeedManyUsers(15);
        var spec = new AllUsersSpecification();

        // Act
        var result = await this._repository.ToPagedListAsync(spec, 2, 5);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(5);
        result.PageNumber.ShouldBe(2);
        result.HasNextPage.ShouldBeTrue();
        result.HasPreviousPage.ShouldBeTrue();
    }

    [Fact]
    public async Task ToPageEnumerable_WithEmptyResult_ShouldReturnEmptyEnumerable()
    {
        // Arrange
        var spec = new ActiveUsersSpecification();

        // Act
        var enumerable = this._repository.ToPageEnumerable(spec);
        var result = new List<User>();
        await foreach (var user in enumerable)
        {
            result.Add(user);
        }

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ToPageEnumerable_WithEntities_ShouldReturnAsyncEnumerable()
    {
        // Arrange
        await this.SeedTestUsers();
        var spec = new ActiveUsersSpecification();

        // Act
        var enumerable = this._repository.ToPageEnumerable(spec);
        var result = new List<User>();
        await foreach (var user in enumerable)
        {
            result.Add(user);
        }

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldAllBe(u => u.FirstName.StartsWith("Active"));
    }

    [Fact]
    public async Task ToPageEnumerable_WithProjection_ShouldReturnAsyncEnumerableOfModels()
    {
        // Arrange
        await this.SeedTestUsers();
        var spec = new ActiveUsersSpecification();

        // Act
        var enumerable = this._repository.ToPageEnumerable<User, UserDto>(spec);
        var result = new List<UserDto>();
        await foreach (var dto in enumerable)
        {
            result.Add(dto);
        }

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldAllBe(dto => !string.IsNullOrEmpty(dto.FullName));
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

    private class AllUsersSpecification : Specification<User>
    {
        #region Constructors

        public AllUsersSpecification()
        {
            this.AddOrderBy(u => u.Id);
        }

        #endregion
    }

    private class UserByFirstNameSpecification : Specification<User>
    {
        #region Constructors

        public UserByFirstNameSpecification(string firstName)
        {
            this.WithFilter(u => u.FirstName == firstName);
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