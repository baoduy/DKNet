using Microsoft.EntityFrameworkCore.Storage;
using EfCore.Repos.Tests.TestEntities;
using Microsoft.EntityFrameworkCore;
using Mapster;

namespace EfCore.Repos.Tests;

public class RepositoryAdvancedTests(RepositoryAdvancedFixture fixture) : IClassFixture<RepositoryAdvancedFixture>
{
    private readonly RepositoryAdvancedFixture _fixture = fixture;

    [Fact]
    public async Task GetProjectionReturnsCorrectProjection()
    {
        // Arrange
        var entity = new User("projtest1") { FirstName = "ProjectMe", LastName = "Test" };
        _fixture.DbContext.Add(entity);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var projection = _fixture.RepositoryWithMapper.GetProjection<UserDto>();
        var result = await projection.Where(u => u.FirstName == "ProjectMe").FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ProjectMe", result.FirstName);
        Assert.Equal("Test", result.LastName);
    }

    [Fact]
    public void GetProjectionThrowsWhenMapperNotRegistered()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _fixture.RepositoryWithoutMapper.GetProjection<UserDto>());
    }

    [Fact]
    public async Task ReadRepositoryGetProjectionReturnsCorrectProjection()
    {
        // Arrange
        var entity = new User("readprojtest1") { FirstName = "ReadProjectMe", LastName = "Test" };
        _fixture.DbContext.Add(entity);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var projection = _fixture.ReadRepositoryWithMapper.GetProjection<UserDto>();
        var result = await projection.Where(u => u.FirstName == "ReadProjectMe").FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ReadProjectMe", result.FirstName);
        Assert.Equal("Test", result.LastName);
    }

    [Fact]
    public void ReadRepositoryGetProjectionThrowsWhenMapperNotRegistered()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _fixture.ReadRepositoryWithoutMapper.GetProjection<UserDto>());
    }

    [Fact]
    public async Task ReadRepositoryGetsReturnsNoTrackingQuery()
    {
        // Arrange
        var entity = new User("tracktest1") { FirstName = "TrackMe", LastName = "Test" };
        _fixture.DbContext.Add(entity);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var query = _fixture.ReadRepositoryWithMapper.Gets();
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
        var query = _fixture.RepositoryWithMapper.Gets();
        var result = await query.Where(u => u.FirstName == "TrackMe2").FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(result);
        var entry = _fixture.DbContext.Entry(result);
        Assert.NotEqual(EntityState.Detached, entry.State);
    }

    [Fact]
    public async Task TransactionRollbackWorksCorrectly()
    {
        // Arrange
        var entity = new User("transtest1") { FirstName = "TransactionTest", LastName = "Test" };

        // Act
        using var transaction = await _fixture.RepositoryWithMapper.BeginTransactionAsync();
        _fixture.RepositoryWithMapper.Add(entity);
        await _fixture.RepositoryWithMapper.SaveChangesAsync();
        
        // Verify entity exists in transaction
        var resultInTransaction = await _fixture.DbContext.Set<User>().FindAsync(entity.Id);
        Assert.NotNull(resultInTransaction);

        await transaction.RollbackAsync();

        // Assert
        var resultAfterRollback = await _fixture.DbContext.Set<User>().FindAsync(entity.Id);
        Assert.Null(resultAfterRollback);
    }

    [Fact]
    public async Task TransactionCommitWorksCorrectly()
    {
        // Arrange
        var entity = new User("transtest2") { FirstName = "TransactionCommit", LastName = "Test" };

        // Act
        using var transaction = await _fixture.RepositoryWithMapper.BeginTransactionAsync();
        _fixture.RepositoryWithMapper.Add(entity);
        await _fixture.RepositoryWithMapper.SaveChangesAsync();
        await transaction.CommitAsync();

        // Assert
        var result = await _fixture.DbContext.Set<User>().FindAsync(entity.Id);
        Assert.NotNull(result);
        Assert.Equal("TransactionCommit", result.FirstName);
    }

    [Fact]
    public async Task FindAsyncWithParamsWorksCorrectly()
    {
        // Arrange
        var entity = new User("findparams1") { FirstName = "FindByParams", LastName = "Test" };
        _fixture.DbContext.Add(entity);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _fixture.RepositoryWithMapper.FindAsync(entity.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("FindByParams", result.FirstName);
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
        _fixture.RepositoryWithMapper.AddRange(entities);
        await _fixture.RepositoryWithMapper.SaveChangesAsync();

        // Assert
        var results = await _fixture.DbContext.Set<User>()
            .Where(u => u.CreatedBy.StartsWith("multi"))
            .ToListAsync();
        Assert.Equal(3, results.Count);
        Assert.Contains(results, r => r.FirstName == "Multi1");
        Assert.Contains(results, r => r.FirstName == "Multi2");
        Assert.Contains(results, r => r.FirstName == "Multi3");
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
        _fixture.RepositoryWithMapper.Update(entity);
        var affectedRows = await _fixture.RepositoryWithMapper.SaveChangesAsync();

        // Assert
        Assert.Equal(1, affectedRows);
        var result = await _fixture.DbContext.Set<User>().FindAsync(entity.Id);
        Assert.Equal("Updated", result?.FirstName);
    }

    [Fact]
    public async Task DeleteAndSaveChangesRemovesEntity()
    {
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
        _fixture.RepositoryWithMapper.UpdateRange(entities);
        var affectedRows = await _fixture.RepositoryWithMapper.SaveChangesAsync();

        // Assert
        Assert.Equal(2, affectedRows);
        var results = await _fixture.DbContext.Set<User>()
            .Where(u => u.CreatedBy.StartsWith("updrange"))
            .ToListAsync();
        Assert.All(results, r => Assert.Equal("Updated", r.LastName));
    }

    [Fact]
    public async Task ConcurrentUpdatesHandledCorrectly()
    {
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
        _fixture.RepositoryWithMapper.Update(entity);
        
        // This should succeed since we're not using row versioning in this simple test
        var affectedRows = await _fixture.RepositoryWithMapper.SaveChangesAsync();
        Assert.Equal(1, affectedRows);
    }
}

public class UserDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}