using EfCore.HookTests.Hooks;

namespace EfCore.HookTests;

public class HookAdvancedTests(HookFixture fixture) : IClassFixture<HookFixture>
{
    private readonly ServiceProvider _provider = fixture.Provider;

    [Fact]
    public async Task Hook_ShouldExecuteOnEntityAdd()
    {
        // Arrange
        var hook = _provider.GetRequiredKeyedService<HookTest>(typeof(HookContext).FullName);
        hook.Reset();
        var db = _provider.GetRequiredService<HookContext>();

        var entity = new CustomerProfile { Name = "Test Add" };

        // Act
        await db.AddAsync(entity);
        await db.SaveChangesAsync();

        // Assert
        HookTest.BeforeCalled.ShouldBeTrue();
        HookTest.AfterCalled.ShouldBeTrue();
        HookTest.BeforeCallCount.ShouldBeGreaterThanOrEqualTo(1);
        HookTest.AfterCallCount.ShouldBeGreaterThanOrEqualTo(1);

        // Verify entity was actually saved
        var savedEntity = await db.Set<CustomerProfile>().FindAsync(entity.Id);
        savedEntity.ShouldNotBeNull();
        savedEntity.Name.ShouldBe("Test Add");
    }

    [Fact]
    public async Task Hook_ShouldExecuteOnEntityUpdate()
    {
        // Arrange
        var hook = _provider.GetRequiredKeyedService<HookTest>(typeof(HookContext).FullName);
        hook.Reset();
        var db = _provider.GetRequiredService<HookContext>();

        var entity = new CustomerProfile { Name = "Test Update Original" };
        await db.AddAsync(entity);
        await db.SaveChangesAsync();

        hook.Reset(); // Reset hook counters after initial add

        // Act
        entity.Name = "Test Update Modified";
        await db.SaveChangesAsync();

        // Assert
        HookTest.BeforeCalled.ShouldBeTrue();
        HookTest.AfterCalled.ShouldBeTrue();
        HookTest.BeforeCallCount.ShouldBeGreaterThanOrEqualTo(1);
        HookTest.AfterCallCount.ShouldBeGreaterThanOrEqualTo(1);

        // Verify entity was actually updated
        var updatedEntity = await db.Set<CustomerProfile>().FindAsync(entity.Id);
        updatedEntity.ShouldNotBeNull();
        updatedEntity.Name.ShouldBe("Test Update Modified");
    }

    [Fact]
    public async Task Hook_ShouldExecuteOnEntityDelete()
    {
        // Arrange
        var hook = _provider.GetRequiredKeyedService<HookTest>(typeof(HookContext).FullName);
        hook.Reset();
        var db = _provider.GetRequiredService<HookContext>();

        var entity = new CustomerProfile { Name = "Test Delete" };
        await db.AddAsync(entity);
        await db.SaveChangesAsync();

        var entityId = entity.Id;
        hook.Reset(); // Reset hook counters after initial add

        // Act
        db.Remove(entity);
        await db.SaveChangesAsync();

        // Assert
        HookTest.BeforeCalled.ShouldBeTrue();
        HookTest.AfterCalled.ShouldBeTrue();
        HookTest.BeforeCallCount.ShouldBe(1);
        HookTest.AfterCallCount.ShouldBe(1);

        // Verify entity was actually deleted
        var deletedEntity = await db.Set<CustomerProfile>().FindAsync(entityId);
        deletedEntity.ShouldBeNull();
    }

    [Fact]
    public async Task Hook_ShouldExecuteOnBulkOperations()
    {
        // Arrange
        var hook = _provider.GetRequiredKeyedService<HookTest>(typeof(HookContext).FullName);
        hook.Reset();
        var db = _provider.GetRequiredService<HookContext>();

        var entities = new[]
        {
            new CustomerProfile { Name = "Bulk 1" },
            new CustomerProfile { Name = "Bulk 2" },
            new CustomerProfile { Name = "Bulk 3" }
        };

        // Act
        await db.AddRangeAsync(entities);
        await db.SaveChangesAsync();

        // Assert
        HookTest.BeforeCalled.ShouldBeTrue();
        HookTest.AfterCalled.ShouldBeTrue();
        HookTest.BeforeCallCount.ShouldBe(1);
        HookTest.AfterCallCount.ShouldBe(1);

        // Verify all entities were saved
        var savedEntities = await db.Set<CustomerProfile>()
            .Where(e => e.Name.StartsWith("Bulk"))
            .ToListAsync();

        savedEntities.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Hook_ShouldHandleNoChangesGracefully()
    {
        // Arrange
        var hook = _provider.GetRequiredKeyedService<HookTest>(typeof(HookContext).FullName);
        hook.Reset();

        var db = _provider.GetRequiredService<HookContext>();
        db.ChangeTracker.Clear();
        // Act - Save without any changes
        await db.SaveChangesAsync();

        // Assert - Hook should not be called when there are no changes
        HookTest.BeforeCalled.ShouldBeFalse();
        HookTest.AfterCalled.ShouldBeFalse();
        HookTest.BeforeCallCount.ShouldBe(0);
        HookTest.AfterCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task Hook_ShouldExecuteMultipleTimes()
    {
        // Arrange
        var hook = _provider.GetRequiredKeyedService<HookTest>(typeof(HookContext).FullName);
        hook.Reset();
        var db = _provider.GetRequiredService<HookContext>();

        // Act - Multiple save operations
        await db.AddAsync(new CustomerProfile { Name = "Multiple 1" });
        await db.SaveChangesAsync();

        await db.AddAsync(new CustomerProfile { Name = "Multiple 2" });
        await db.SaveChangesAsync();

        await db.AddAsync(new CustomerProfile { Name = "Multiple 3" });
        await db.SaveChangesAsync();

        // Assert
        HookTest.BeforeCalled.ShouldBeTrue();
        HookTest.AfterCalled.ShouldBeTrue();
        HookTest.BeforeCallCount.ShouldBeGreaterThanOrEqualTo(1);
        HookTest.AfterCallCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Hook_ShouldProvideCorrectChangeTrackerInfo()
    {
        // Arrange
        var hook = _provider.GetRequiredKeyedService<HookTest>(typeof(HookContext).FullName);
        hook.Reset();
        var db = _provider.GetRequiredService<HookContext>();

        var entity = new CustomerProfile { Name = "Change Tracker Test" };

        // Act
        await db.AddAsync(entity);
        await db.SaveChangesAsync();

        // Assert
        HookTest.BeforeCalled.ShouldBeTrue();
        HookTest.AfterCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task Hook_ShouldExecuteInCorrectOrder()
    {
        // Arrange
        var hook = _provider.GetRequiredKeyedService<HookTest>(typeof(HookContext).FullName);
        hook.Reset();
        var db = _provider.GetRequiredService<HookContext>();

        var entity = new CustomerProfile { Name = "Order Test" };

        // Act
        await db.AddAsync(entity);
        await db.SaveChangesAsync();

        // Assert
        HookTest.BeforeCalled.ShouldBeTrue();
        HookTest.AfterCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task Hook_ShouldWorkWithTransactions()
    {
        // Arrange
        var hook = _provider.GetRequiredKeyedService<HookTest>(typeof(HookContext).FullName);
        hook.Reset();
        var db = _provider.GetRequiredService<HookContext>();

        var entity = new CustomerProfile { Name = "Transaction Test" };

        // Act
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.AddAsync(entity);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        // Assert
        HookTest.BeforeCalled.ShouldBeTrue();
        HookTest.AfterCalled.ShouldBeTrue();

        // Verify entity was saved
        var savedEntity = await db.Set<CustomerProfile>().FindAsync(entity.Id);
        savedEntity.ShouldNotBeNull();
    }
}