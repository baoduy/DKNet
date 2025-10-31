namespace EfCore.Repos.Tests;

public class RepositoryAdvancedTests(RepositoryAdvancedFixture fixture) : IClassFixture<RepositoryAdvancedFixture>
{
    #region Fields

    private readonly RepositoryAdvancedFixture _fixture = fixture;

    #endregion

    #region Methods

    [Fact]
    public async Task ConcurrentUpdatesHandledCorrectly()
    {
        _fixture.DbContext.ChangeTracker.Clear();

        // Arrange
        var entity = new User("concurtest") { FirstName = "Concurrent", LastName = "Test" };
        _fixture.DbContext.Add(entity);
        await _fixture.DbContext.SaveChangesAsync();

        // Simulate concurrent update
        var context2 = new TestDbContext(_fixture.DbContext.Options);
        var entity2 = await context2.Set<User>().FindAsync(entity.Id);
        entity2!.FirstName = "Updated by context2";
        await context2.SaveChangesAsync();
        await context2.DisposeAsync();

        // Act & Assert
        entity.FirstName = "Updated by context1";
        await _fixture.RepositoryWithMapper.UpdateAsync(entity);

        // This should succeed since we're not using row versioning in this simple test
        var affectedRows = await _fixture.RepositoryWithMapper.SaveChangesAsync();
        Assert.Equal(1, affectedRows);
    }

    [Fact]
    public async Task DeleteAndSaveChangesRemovesEntity()
    {
        _fixture.DbContext.ChangeTracker.Clear();

        // Arrange
        var entity = new User("deletetest") { FirstName = "ToDelete", LastName = "Test" };
        _fixture.DbContext.Add(entity);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        _fixture.RepositoryWithMapper.Delete(entity);
        var affectedRows = await _fixture.RepositoryWithMapper.SaveChangesAsync();

        // Assert
        Assert.Equal(1, affectedRows);
        var result = await _fixture.DbContext.Set<User>().FindAsync(entity.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteRangeAndSaveChangesRemovesMultipleEntities()
    {
        _fixture.DbContext.ChangeTracker.Clear();

        // Arrange
        var entities = new[]
        {
            new User("delrange1") { FirstName = "DelRange1", LastName = "Test" },
            new User("delrange2") { FirstName = "DelRange2", LastName = "Test" }
        };
        _fixture.DbContext.AddRange(entities);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        _fixture.RepositoryWithMapper.DeleteRange(entities);
        var affectedRows = await _fixture.RepositoryWithMapper.SaveChangesAsync();

        // Assert
        Assert.Equal(2, affectedRows);
        var results = await _fixture.DbContext.Set<User>()
            .Where(u => u.CreatedBy.StartsWith("delrange"))
            .ToListAsync();
        Assert.Empty(results);
    }

    [Fact]
    public async Task FindAsyncWithParamsReturnsNullWhenNotFound()
    {
        // Act
        var result = await _fixture.RepositoryWithMapper.FindAsync(999999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task FindAsyncWithParamsWorksCorrectly()
    {
        _fixture.DbContext.ChangeTracker.Clear();

        // Arrange
        var entity = new User("findparams1") { FirstName = "FindByParams", LastName = "Test" };
        _fixture.DbContext.Add(entity);

        var entity2 = new User("findparams2") { FirstName = "FindByParams2", LastName = "Test2" };
        _fixture.DbContext.Add(entity2);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _fixture.RepositoryWithMapper.FindAsync(entity.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("FindByParams", result.FirstName);
    }

    [Fact]
    public async Task GetProjectionReturnsCorrectProjection()
    {
        // Arrange
        var entity = new User("projtest1") { FirstName = "ProjectMe", LastName = "Test" };
        _fixture.DbContext.Add(entity);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var projection = _fixture.RepositoryWithMapper.Query<UserDto>(u => u.FirstName == "ProjectMe");
        var result = await projection.FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ProjectMe", result.FirstName);
        Assert.Equal("Test", result.LastName);
    }

    [Fact]
    public void GetProjectionThrowsWhenMapperNotRegistered()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _fixture.RepositoryWithoutMapper.Query<UserDto>(u => u.FirstName == "ProjectMe"));
    }

    [Fact]
    public async Task MultipleEntitiesCanBeAddedAndRetrieved()
    {
        // Arrange
        var entities = new[]
        {
            new User("multi1") { FirstName = "Multi1", LastName = "Test" },
            new User("multi2") { FirstName = "Multi2", LastName = "Test" },
            new User("multi3") { FirstName = "Multi3", LastName = "Test" }
        };

        // Act
        await _fixture.RepositoryWithMapper.AddRangeAsync(entities);
        await _fixture.RepositoryWithMapper.SaveChangesAsync();

        // Assert
        var results = await _fixture.DbContext.Set<User>()
            .Where(u => u.CreatedBy.StartsWith("multi"))
            .ToListAsync();
        Assert.Equal(3, results.Count);
        Assert.Contains(results, r => string.Equals(r.FirstName, "Multi1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, r => string.Equals(r.FirstName, "Multi2", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, r => string.Equals(r.FirstName, "Multi3", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadRepositoryGetProjectionReturnsCorrectProjection()
    {
        // Arrange
        var entity = new User("readprojtest1") { FirstName = "ReadProjectMe", LastName = "Test" };
        _fixture.DbContext.Add(entity);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        var p1 = _fixture.ReadRepositoryWithMapper.Query(u => u.Id == entity.Id);
        var r1 = await p1.FirstOrDefaultAsync();
        Assert.NotNull(r1);

        // Act
        var projection = _fixture.ReadRepositoryWithMapper.Query<UserDto>(u => u.FirstName == "ReadProjectMe");
        var result = await projection.FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ReadProjectMe", result.FirstName);
        Assert.Equal("Test", result.LastName);
    }

    [Fact]
    public void ReadRepositoryGetProjectionThrowsWhenMapperNotRegistered()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _fixture.ReadRepositoryWithoutMapper.Query<UserDto>(u => u.FirstName == "ProjectMe"));
    }

    [Fact]
    public async Task ReadRepositoryGetsReturnsNoTrackingQuery()
    {
        // Arrange
        var entity = new User("tracktest1") { FirstName = "TrackMe", LastName = "Test" };
        _fixture.DbContext.Add(entity);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var query = _fixture.ReadRepositoryWithMapper.Query();
        var result = await query.Where(u => u.FirstName == "TrackMe").FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(result);
        var entry = _fixture.DbContext.Entry(result);
        Assert.Equal(EntityState.Detached, entry.State);
    }

    [Fact]
    public async Task RepositoryGetsReturnsTrackingQuery()
    {
        // Arrange
        var entity = new User("tracktest2") { FirstName = "TrackMe2", LastName = "Test" };
        _fixture.DbContext.Add(entity);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var query = _fixture.RepositoryWithMapper.Query();
        var result = await query.Where(u => u.FirstName == "TrackMe2").FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(result);
        var entry = _fixture.DbContext.Entry(result);
        Assert.NotEqual(EntityState.Detached, entry.State);
    }

    [Fact]
    public async Task TransactionCommitWorksCorrectly()
    {
        // Arrange
        var entity = new User("transtest2") { FirstName = "TransactionCommit", LastName = "Test" };

        // Act
        await using var transaction = await _fixture.RepositoryWithMapper.BeginTransactionAsync();
        await _fixture.RepositoryWithMapper.AddAsync(entity);
        await _fixture.RepositoryWithMapper.SaveChangesAsync();
        await transaction.CommitAsync();

        // Assert
        var result = await _fixture.DbContext.Set<User>().FindAsync(entity.Id);
        Assert.NotNull(result);
        Assert.Equal("TransactionCommit", result.FirstName);
    }

    [Fact]
    public async Task TransactionRollbackWorksCorrectly()
    {
        // Arrange
        var entity = new User("transtest1") { FirstName = "TransactionTest", LastName = "Test" };

        // Act
        await using var transaction = await _fixture.RepositoryWithMapper.BeginTransactionAsync();
        await _fixture.RepositoryWithMapper.AddAsync(entity);
        await _fixture.RepositoryWithMapper.SaveChangesAsync();

        // Verify entity exists in transaction
        var resultInTransaction = await _fixture.DbContext.Set<User>().FindAsync(entity.Id);
        Assert.NotNull(resultInTransaction);

        await transaction.RollbackAsync();

        // Clear the change tracker to avoid stale data
        //_fixture.DbContext.ChangeTracker.Clear();
        // Assert
        var resultAfterRollback = await _fixture.DbContext.Set<User>().Where(i => i.Id == entity.Id).ToListAsync();
        Assert.True(resultAfterRollback.Count == 0);
    }

    [Fact]
    public async Task UpdateAndSaveChangesUpdatesEntity()
    {
        // Arrange
        var entity = new User("updatetest") { FirstName = "Original", LastName = "Test" };
        _fixture.DbContext.Add(entity);
        await _fixture.DbContext.SaveChangesAsync();

        // Detach to simulate external change
        _fixture.DbContext.Entry(entity).State = EntityState.Detached;
        entity.FirstName = "Updated";

        // Act
        await _fixture.RepositoryWithMapper.UpdateAsync(entity);
        var affectedRows = await _fixture.RepositoryWithMapper.SaveChangesAsync();

        // Assert
        Assert.Equal(1, affectedRows);
        var result = await _fixture.DbContext.Set<User>().FindAsync(entity.Id);
        Assert.Equal("Updated", result?.FirstName);
    }

    [Fact]
    public async Task UpdateRangeAndSaveChangesUpdatesMultipleEntities()
    {
        // Arrange
        var entities = new[]
        {
            new User("updrange1") { FirstName = "UpdRange1", LastName = "Original" },
            new User("updrange2") { FirstName = "UpdRange2", LastName = "Original" }
        };
        _fixture.DbContext.AddRange(entities);
        await _fixture.DbContext.SaveChangesAsync();

        // Detach entities to simulate external changes
        foreach (var entity in entities)
        {
            _fixture.DbContext.Entry(entity).State = EntityState.Detached;
            entity.LastName = "Updated";
        }

        // Act
        await _fixture.RepositoryWithMapper.UpdateRangeAsync(entities);
        var affectedRows = await _fixture.RepositoryWithMapper.SaveChangesAsync();

        // Assert
        Assert.Equal(2, affectedRows);
        var results = await _fixture.DbContext.Set<User>()
            .Where(u => u.CreatedBy.StartsWith("updrange"))
            .ToListAsync();
        Assert.All(results, r => Assert.Equal("Updated", r.LastName));
    }

    #endregion
}

public class UserDto
{
    #region Properties

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    #endregion
}