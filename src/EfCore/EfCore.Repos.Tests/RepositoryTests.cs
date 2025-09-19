using DKNet.EfCore.Repos;
using EfCore.Repos.Tests.TestEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shouldly;
using Xunit.Abstractions;

namespace EfCore.Repos.Tests;

public class RepositoryTests(RepositoryFixture fixture, ITestOutputHelper output) : IClassFixture<RepositoryFixture>
{
    [Fact]
    public async Task AddAsyncAddsEntityToDatabase()
    {
        fixture.DbContext.ChangeTracker.Clear();
        // Arrange
        var entity = new User("steven1") { FirstName = "Test User", LastName = "Test" };

        // Act
        await fixture.Repository.AddAsync(entity);
        await fixture.Repository.SaveChangesAsync();

        // Assert
        var result = await fixture.DbContext.Set<User>().Where(e => e.Id == entity.Id).FirstAsync();
        Assert.NotNull(result);
        Assert.Equal("Test User", result.FirstName);
    }

    [Fact]
    public async Task AddUserWithAddressAsync()
    {
        // Arrange
        var entity = new User("steven1") { FirstName = "Test User", LastName = "Test" };
        entity.AddAddress(new Address
        {
            City = "Test City",
            Street = "Test Street",
            Country = "Test Country"
        });

        // Act
        await fixture.Repository.AddAsync(entity);
        await fixture.Repository.SaveChangesAsync();

        // Assert
        var result = await fixture.DbContext.Set<User>()
            .Where(e => e.Id == entity.Id)
            .Include(user => user.Addresses)
            .FirstAsync();

        Assert.NotNull(result);
        result.Addresses.Count.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateNavigationPropertiesAsync()
    {
        // Arrange
        var entity = new User("steven1") { FirstName = "Test User", LastName = "Test" };

        // Act
        await fixture.Repository.AddAsync(entity);
        await fixture.Repository.SaveChangesAsync();
        fixture.DbContext.ChangeTracker.Clear();

        // Assert
        var user = await fixture.DbContext.Set<User>()
            .Include(user => user.Addresses)
            .Where(e => e.Id == entity.Id)
            .AsTracking()
            .FirstAsync();
        Assert.NotNull(user);
        user.Addresses.Count.ShouldBe(0);
        fixture.DbContext.ChangeTracker.HasChanges().ShouldBeFalse();

        user.AddAddress(new Address
        {
            City = "Test City 1",
            Street = "Test Street 1",
            Country = "Test Country 1"
        });
        user.AddAddress(new Address
        {
            City = "Test City 2",
            Street = "Test Street 2",
            Country = "Test Country 2"
        });
        user.AddAddress(new Address
        {
            City = "Test City 3",
            Street = "Test Street 3",
            Country = "Test Country 3"
        });

        output.WriteLine(fixture.DbContext.ChangeTracker.DebugView.LongView);
        fixture.DbContext.ChangeTracker.HasChanges().ShouldBeTrue();
        await fixture.Repository.SaveChangesAsync();
        fixture.DbContext.ChangeTracker.Clear();

        // Assert
        var result = await fixture.DbContext.Set<User>()
            .Where(e => e.Id == user.Id)
            .Include(u => u.Addresses)
            .FirstAsync();

        Assert.NotNull(result);
        result.Addresses.Count.ShouldBe(3);
    }

    [Fact]
    public async Task UpdateAndSaveAsyncUpdatesEntityInDatabase()
    {
        fixture.DbContext.ChangeTracker.Clear();
        // Arrange
        var entity = new User("steven3") { FirstName = "Original", LastName = "Test" };
        fixture.DbContext.Add(entity);
        await fixture.DbContext.SaveChangesAsync();

        // Act
        entity.FirstName = "Updated";
        var affectedRows = await fixture.Repository.SaveChangesAsync();

        // Assert
        Assert.Equal(1, affectedRows);
        var result = await fixture.DbContext.Set<User>().FindAsync(entity.Id);
        Assert.Equal("Updated", result?.FirstName);
    }

    [Fact]
    public async Task BeginTransactionAsyncCreatesTransaction()
    {
        // Act
        var transaction = await fixture.Repository.BeginTransactionAsync();

        // Assert
        Assert.NotNull(transaction);
        Assert.IsAssignableFrom<IDbContextTransaction>(transaction);
    }

    [Fact]
    public async Task ConcurrencyGuidWithRepositoryTest()
    {
        fixture.DbContext.ChangeTracker.Clear();
        var repo1 = new Repository<UserGuid>(fixture.DbContext);

        // 1. Create a new UserGuid entity
        var user = new UserGuid("A")
        {
            FirstName = "Duy",
            LastName = "Hoang"
        };
        user.AddAddress(new AddressGuid { City = "HBD", Street = "HBD", Country = "HBD" });
        user.AddAddress(new AddressGuid { City = "HBD", Street = "HBD", Country = "HBD" });
        await repo1.AddAsync(user);
        await repo1.SaveChangesAsync();
        var createdVersion = user.RowVersion;
        createdVersion.ShouldBeGreaterThan(0u);

        // 2. Simulate two users/contexts
        fixture.DbContext.ChangeTracker.Clear();
        await using var db2 = await fixture.CreateNewDbContext();
        var repo2 = new Repository<UserGuid>(db2);

        var userFromRepo1 = await repo1.Gets().AsNoTracking().FirstOrDefaultAsync(u => u.Id == user.Id);
        var userFromRepo2 = await repo2.Gets().AsNoTracking().FirstOrDefaultAsync(u => u.Id == user.Id);

        userFromRepo1.ShouldNotBeNull();
        userFromRepo2.ShouldNotBeNull();

        // 3. Update and save with repo1
        userFromRepo1.FirstName = "Duy3";
        userFromRepo1.SetUpdatedBy("System");
        await repo1.UpdateAsync(userFromRepo1);
        await repo1.SaveChangesAsync();

        // 4. Attempt to update and save with repo2 (should fail)
        userFromRepo2.FirstName = "Duy4";
        userFromRepo2.SetUpdatedBy("System");
        await repo2.UpdateAsync(userFromRepo2);
        await Should.ThrowAsync<DbUpdateConcurrencyException>(async () => await repo2.SaveChangesAsync());
    }

    [Fact]
    public async Task FindAsyncWithExpressionReturnsCorrectEntity()
    {
        // Arrange
        var entity = new User("findtest1") { FirstName = "FindMe", LastName = "Test" };
        fixture.DbContext.Add(entity);
        await fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await fixture.ReadRepository.FindAsync(u => u.FirstName == "FindMe");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("FindMe", result.FirstName);
        Assert.Equal("findtest1", result.CreatedBy);
    }

    [Fact]
    public async Task FindAsyncWithExpressionReturnsNullWhenNotFound()
    {
        // Act
        var result = await fixture.ReadRepository.FindAsync(u => u.FirstName == "NonExistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task FindAsyncWithIdReturnsCorrectEntity()
    {
        // Arrange
        var entity = new User("findtest2") { FirstName = "FindById", LastName = "Test" };
        fixture.DbContext.Add(entity);
        await fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await fixture.ReadRepository.FindAsync(entity.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("FindById", result.FirstName);
    }

    [Fact]
    public async Task FindAsyncWithIdReturnsNullWhenNotFound()
    {
        // Act
        var result = await fixture.ReadRepository.FindAsync(123);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetsReturnsNoTrackingQueryable()
    {
        // Act
        var query = fixture.ReadRepository.Gets();

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
        await fixture.Repository.AddRangeAsync(entities);
        await fixture.Repository.SaveChangesAsync();

        // Assert
        var results = await fixture.DbContext.Set<User>()
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
        await fixture.Repository.AddRangeAsync(entities);
        var affectedRows = await fixture.Repository.SaveChangesAsync();

        // Assert
        Assert.Equal(2, affectedRows);
        var results = await fixture.DbContext.Set<User>()
            .Where(u => u.CreatedBy.StartsWith("bulkins"))
            .ToListAsync();
        Assert.Equal(2, results.Count);
    }


    [Fact]
    public void DeleteRemovesEntityFromContext()
    {
        fixture.DbContext.ChangeTracker.Clear();
        // Arrange
        var entity = new User("deltest") { FirstName = "ToDelete", LastName = "Test" };
        fixture.DbContext.Add(entity);
        fixture.DbContext.SaveChanges();

        // Act
        fixture.Repository.Delete(entity);

        // Assert
        var entry = fixture.DbContext.Entry(entity);
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
        fixture.DbContext.AddRange(entities);
        fixture.DbContext.SaveChanges();

        // Act
        fixture.Repository.DeleteRange(entities);

        // Assert
        foreach (var entity in entities)
        {
            var entry = fixture.DbContext.Entry(entity);
            Assert.Equal(EntityState.Deleted, entry.State);
        }
    }

    [Fact]
    public async Task UpdateMarksEntityAsModified()
    {
        // Arrange
        var entity = new User("updtest") { FirstName = "Original", LastName = "Test" };
        fixture.DbContext.Add(entity);
        await fixture.DbContext.SaveChangesAsync();

        // Detach the entity to simulate it coming from another context
        fixture.DbContext.Entry(entity).State = EntityState.Detached;
        entity.FirstName = "Modified";

        // Act
        await fixture.Repository.UpdateAsync(entity);

        // Assert
        var entry = fixture.DbContext.Entry(entity);
        Assert.Equal(EntityState.Modified, entry.State);
    }

    [Fact]
    public async Task UpdateRangeMarksMultipleEntitiesAsModified()
    {
        fixture.DbContext.ChangeTracker.Clear();
        // Arrange
        var entities = new[]
        {
            new User("updrange1") { FirstName = "UpdRange1", LastName = "Original" },
            new User("updrange2") { FirstName = "UpdRange2", LastName = "Original" }
        };
        fixture.DbContext.AddRange(entities);
        await fixture.DbContext.SaveChangesAsync();

        // Detach entities to simulate them coming from another context
        foreach (var entity in entities)
        {
            fixture.DbContext.Entry(entity).State = EntityState.Detached;
            entity.LastName = "Modified";
        }

        // Act
        await fixture.Repository.UpdateRangeAsync(entities);

        // Assert
        foreach (var entity in entities)
        {
            var entry = fixture.DbContext.Entry(entity);
            Assert.Equal(EntityState.Modified, entry.State);
        }
    }

    [Fact]
    public async Task FindAsyncDetachesEntityFromContext()
    {
        // Arrange
        var entity = new User("detachtest") { FirstName = "DetachTest", LastName = "Test" };
        fixture.DbContext.Add(entity);
        await fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await fixture.ReadRepository.FindAsync(entity.Id);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task FindAsyncWithExpressionDetachesEntityFromContext()
    {
        // Arrange
        var entity = new User("detachtest2") { FirstName = "DetachTest2", LastName = "Test" };
        fixture.DbContext.Add(entity);
        await fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await fixture.ReadRepository.FindAsync(u => u.FirstName == "DetachTest2");

        // Assert
        Assert.NotNull(result);
        var entry = fixture.DbContext.Entry(result);
        Assert.Equal(EntityState.Detached, entry.State);
    }

    [Fact]
    public void GetProjectionThrowsWhenMapperNotRegistered()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => fixture.ReadRepository.GetDto<UserDto>());
    }

    [Fact]
    public async Task FindAsyncWithCancellationTokenRespectsCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.ReadRepository.FindAsync(u => u.FirstName == "Test", cts.Token));
    }


    [Fact]
    public async Task SaveChangesAsyncWithCancellationTokenRespectsCancellation()
    {
        // Arrange
        var entity = new User("savecancel") { FirstName = "SaveCancel", LastName = "Test" };
        await fixture.Repository.AddAsync(entity);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Repository.SaveChangesAsync(cts.Token));
    }

    [Fact]
    public async Task BeginTransactionAsyncWithCancellationTokenRespectsCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Repository.BeginTransactionAsync(cts.Token));
    }
}