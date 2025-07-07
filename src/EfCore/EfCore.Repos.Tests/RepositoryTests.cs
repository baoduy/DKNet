using Microsoft.EntityFrameworkCore.Storage;
using EfCore.Repos.Tests.TestEntities;
using Microsoft.EntityFrameworkCore;

namespace EfCore.Repos.Tests;

public class RepositoryTests(RepositoryFixture fixture) : IClassFixture<RepositoryFixture>
{
    private readonly RepositoryFixture _fixture = fixture;

    [Fact]
    public async Task AddAsyncAddsEntityToDatabase()
    {
        // Arrange
        var entity = new User("steven1") { FirstName = "Test User", LastName = "Test" };

        // Act
        await _fixture.Repository.AddAsync(entity);
        await _fixture.Repository.SaveChangesAsync();

        // Assert
        var result = await _fixture.DbContext.Set<User>().FindAsync(entity.Id);
        Assert.NotNull(result);
        Assert.Equal("Test User", result.FirstName);
    }

    [Fact]
    public async Task UpdateAndSaveAsyncUpdatesEntityInDatabase()
    {
        // Arrange
        var entity = new User("steven3") { FirstName = "Original", LastName = "Test" };
        _fixture.DbContext.Add(entity);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        entity.FirstName = "Updated";
        var affectedRows = await _fixture.Repository.SaveChangesAsync();

        // Assert
        Assert.Equal(1, affectedRows);
        var result = await _fixture.DbContext.Set<User>().FindAsync(entity.Id);
        Assert.Equal("Updated", result?.FirstName);
    }

    [Fact]
    public async Task BeginTransactionAsyncCreatesTransaction()
    {
        // Act
        var transaction = await _fixture.Repository.BeginTransactionAsync();

        // Assert
        Assert.NotNull(transaction);
        Assert.IsAssignableFrom<IDbContextTransaction>(transaction);
    }

    // [Fact]
    // public async Task UpdateRowVersionUpdatesConcurrencyToken()
    // {
    //     // Arrange
    //     var entity = new User("steven4") { FirstName = "Test", LastName = "Test" };
    //     _fixture.DbContext.Add(entity);
    //     await _fixture.DbContext.SaveChangesAsync();

    //     // Act
    //     var newVersion = new byte[] { 1, 2, 3 };
    //     _fixture.Repository.UpdateRowVersion(entity, newVersion);

    //     // Assert
    //     Assert.Equal(newVersion, entity.RowVersion);
    // }

    [Fact]
    public async Task FindAsyncWithExpressionReturnsCorrectEntity()
    {
        // Arrange
        var entity = new User("findtest1") { FirstName = "FindMe", LastName = "Test" };
        _fixture.DbContext.Add(entity);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _fixture.ReadRepository.FindAsync(u => u.FirstName == "FindMe");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("FindMe", result.FirstName);
        Assert.Equal("findtest1", result.CreatedBy);
    }

    [Fact]
    public async Task FindAsyncWithExpressionReturnsNullWhenNotFound()
    {
        // Act
        var result = await _fixture.ReadRepository.FindAsync(u => u.FirstName == "NonExistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task FindAsyncWithIdReturnsCorrectEntity()
    {
        // Arrange
        var entity = new User("findtest2") { FirstName = "FindById", LastName = "Test" };
        _fixture.DbContext.Add(entity);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _fixture.ReadRepository.FindAsync(entity.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("FindById", result.FirstName);
    }

    [Fact]
    public async Task FindAsyncWithIdReturnsNullWhenNotFound()
    {
        // Act
        var result = await _fixture.ReadRepository.FindAsync(123);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetsReturnsNoTrackingQueryable()
    {
        // Act
        var query = _fixture.ReadRepository.Gets();

        // Assert
        Assert.NotNull(query);
        Assert.IsAssignableFrom<IQueryable<User>>(query);
    }

    [Fact]
    public async Task AddRangeAddsMultipleEntities()
    {
        // Arrange
        var entities = new[]
        {
            new User("bulk1") { FirstName = "Bulk1", LastName = "Test" },
            new User("bulk2") { FirstName = "Bulk2", LastName = "Test" },
            new User("bulk3") { FirstName = "Bulk3", LastName = "Test" }
        };

        // Act
        await _fixture.Repository.AddRangeAsync(entities);
        await _fixture.Repository.SaveChangesAsync();

        // Assert
        var results = await _fixture.DbContext.Set<User>()
            .Where(u => u.CreatedBy.StartsWith("bulk"))
            .ToListAsync();
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task BulkInsertAsyncAddsMultipleEntities()
    {
        // Arrange
        var entities = new[]
        {
            new User("bulkins1") { FirstName = "BulkIns1", LastName = "Test" },
            new User("bulkins2") { FirstName = "BulkIns2", LastName = "Test" }
        };

        // Act
        await _fixture.Repository.AddRangeAsync(entities);
        var affectedRows = await _fixture.Repository.SaveChangesAsync();

        // Assert
        Assert.Equal(2, affectedRows);
        var results = await _fixture.DbContext.Set<User>()
            .Where(u => u.CreatedBy.StartsWith("bulkins"))
            .ToListAsync();
        Assert.Equal(2, results.Count);
    }


    [Fact]
    public void DeleteRemovesEntityFromContext()
    {
        // Arrange
        var entity = new User("deltest") { FirstName = "ToDelete", LastName = "Test" };
        _fixture.DbContext.Add(entity);
        _fixture.DbContext.SaveChanges();

        // Act
        _fixture.Repository.Delete(entity);

        // Assert
        var entry = _fixture.DbContext.Entry(entity);
        Assert.Equal(EntityState.Deleted, entry.State);
    }

    [Fact]
    public void DeleteRangeRemovesMultipleEntitiesFromContext()
    {
        // Arrange
        var entities = new[]
        {
            new User("delrange1") { FirstName = "DelRange1", LastName = "Test" },
            new User("delrange2") { FirstName = "DelRange2", LastName = "Test" }
        };
        _fixture.DbContext.AddRange(entities);
        _fixture.DbContext.SaveChanges();

        // Act
        _fixture.Repository.DeleteRange(entities);

        // Assert
        foreach (var entity in entities)
        {
            var entry = _fixture.DbContext.Entry(entity);
            Assert.Equal(EntityState.Deleted, entry.State);
        }
    }

    [Fact]
    public void UpdateMarksEntityAsModified()
    {
        // Arrange
        var entity = new User("updtest") { FirstName = "Original", LastName = "Test" };
        _fixture.DbContext.Add(entity);
        _fixture.DbContext.SaveChanges();

        // Detach the entity to simulate it coming from another context
        _fixture.DbContext.Entry(entity).State = EntityState.Detached;
        entity.FirstName = "Modified";

        // Act
        _fixture.Repository.Update(entity);

        // Assert
        var entry = _fixture.DbContext.Entry(entity);
        Assert.Equal(EntityState.Modified, entry.State);
    }

    [Fact]
    public void UpdateRangeMarksMultipleEntitiesAsModified()
    {
        // Arrange
        var entities = new[]
        {
            new User("updrange1") { FirstName = "UpdRange1", LastName = "Original" },
            new User("updrange2") { FirstName = "UpdRange2", LastName = "Original" }
        };
        _fixture.DbContext.AddRange(entities);
        _fixture.DbContext.SaveChanges();

        // Detach entities to simulate them coming from another context
        foreach (var entity in entities)
        {
            _fixture.DbContext.Entry(entity).State = EntityState.Detached;
            entity.LastName = "Modified";
        }

        // Act
        _fixture.Repository.UpdateRange(entities);

        // Assert
        foreach (var entity in entities)
        {
            var entry = _fixture.DbContext.Entry(entity);
            Assert.Equal(EntityState.Modified, entry.State);
        }
    }

    [Fact]
    public async Task FindAsyncDetachesEntityFromContext()
    {
        // Arrange
        var entity = new User("detachtest") { FirstName = "DetachTest", LastName = "Test" };
        _fixture.DbContext.Add(entity);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _fixture.ReadRepository.FindAsync(entity.Id);

        // Assert
        Assert.NotNull(result);
        var entry = _fixture.DbContext.Entry(result);
        Assert.Equal(EntityState.Detached, entry.State);
    }

    [Fact]
    public async Task FindAsyncWithExpressionDetachesEntityFromContext()
    {
        // Arrange
        var entity = new User("detachtest2") { FirstName = "DetachTest2", LastName = "Test" };
        _fixture.DbContext.Add(entity);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _fixture.ReadRepository.FindAsync(u => u.FirstName == "DetachTest2");

        // Assert
        Assert.NotNull(result);
        var entry = _fixture.DbContext.Entry(result);
        Assert.Equal(EntityState.Detached, entry.State);
    }

    [Fact]
    public void GetProjectionThrowsWhenMapperNotRegistered()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _fixture.ReadRepository.GetProjection<UserDto>());
    }

    [Fact]
    public async Task FindAsyncWithCancellationTokenRespectsCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            _fixture.ReadRepository.FindAsync(u => u.FirstName == "Test", cts.Token));
    }


    [Fact]
    public async Task SaveChangesAsyncWithCancellationTokenRespectsCancellation()
    {
        // Arrange
        var entity = new User("savecancel") { FirstName = "SaveCancel", LastName = "Test" };
        await _fixture.Repository.AddAsync(entity);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => _fixture.Repository.SaveChangesAsync(cts.Token));
    }

    [Fact]
    public async Task BeginTransactionAsyncWithCancellationTokenRespectsCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => _fixture.Repository.BeginTransactionAsync(cts.Token));
    }
}