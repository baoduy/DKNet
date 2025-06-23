using Microsoft.EntityFrameworkCore.Storage;
using EfCore.TestDataLayer;
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
        _fixture.Repository.Add(entity);
        await _fixture.Repository.SaveChangesAsync();

        // Assert
        var result = await _fixture.DbContext.Set<User>().FindAsync(entity.Id);
        Assert.NotNull(result);
        Assert.Equal("Test User", result.FirstName);
    }

    [Fact]
    public async Task DeleteByIdAsyncRemovesEntityFromDatabase()
    {
        // Arrange
        var entity = new User("steven2") { FirstName = "To Delete", LastName = "Test" };
        _fixture.DbContext.Add(entity);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var count = await _fixture.Repository.BulkDeleteAsync(e => e.Id == entity.Id);
        Assert.Equal(1, count);

        var result = await _fixture.DbContext.Set<User>().Where(e => e.Id == entity.Id).FirstOrDefaultAsync();
        Assert.Null(result);
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
}
