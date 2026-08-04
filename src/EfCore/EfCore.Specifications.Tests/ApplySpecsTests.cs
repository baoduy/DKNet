using System.Linq.Expressions;
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace EfCore.Specifications.Tests;

public class ApplySpecsTests
{
    #region Test Setup

    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public int? CategoryId { get; set; }
        public TestCategory? Category { get; set; }
    }

    private class TestCategory
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

    private class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
        public DbSet<TestEntity> Entities => Set<TestEntity>();
        public DbSet<TestCategory> Categories => Set<TestCategory>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestEntity>().HasKey(e => e.Id);
            modelBuilder.Entity<TestCategory>().HasKey(e => e.Id);
        }
    }

    private class TestSpecification : Specification<TestEntity>
    {
        public TestSpecification() { }
        public TestSpecification(Expression<Func<TestEntity, bool>> filter) : base(filter) { }

        public void AddTestInclude(Expression<Func<TestEntity, object?>> include) => AddInclude(include);
        public void AddTestOrderBy(Expression<Func<TestEntity, object>> orderBy) => AddOrderBy(orderBy);
        public void AddTestOrderByDescending(Expression<Func<TestEntity, object>> orderByDesc) =>
            AddOrderByDescending(orderByDesc);
        public void TestIgnoreQueryFilters() => IgnoreQueryFilters();
    }

    #endregion

    #region Methods

    [Fact]
    public void ApplySpecs_WithNullSpecification_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite("DataSource=:memory:").Options;
        using var db = new TestDbContext(options);
        var query = db.Entities.AsQueryable();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => query.ApplySpecs<TestEntity>(null!));
    }

    [Fact]
    public async Task ApplySpecs_WithBasicSpecification_ShouldApplyFilter()
    {
        // Arrange
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options;
        using var db = new TestDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Entities.Add(new TestEntity { Id = 1, Name = "A" });
        db.Entities.Add(new TestEntity { Id = 2, Name = "B" });
        await db.SaveChangesAsync();

        var spec = new TestSpecification(e => e.Name == "A");

        // Act
        var result = db.Entities.ApplySpecs(spec).ToList();

        // Assert
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("A");
    }

    [Fact]
    public async Task ApplySpecs_WithIncludes_ShouldApplyIncludes()
    {
        // Arrange
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options;
        using var db = new TestDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var cat = new TestCategory { Id = 1, Name = "Cat1" };
        db.Categories.Add(cat);
        db.Entities.Add(new TestEntity { Id = 1, Name = "A", CategoryId = 1, Category = cat });
        await db.SaveChangesAsync();

        var spec = new TestSpecification();
        spec.AddTestInclude(e => e.Category);

        // Act
        var result = db.Entities.ApplySpecs(spec).First();

        // Assert
        result.Category.ShouldNotBeNull();
        result.Category.Name.ShouldBe("Cat1");
    }

    [Fact]
    public async Task ApplySpecs_WithOrdering_ShouldApplyOrder()
    {
        // Arrange
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options;
        using var db = new TestDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Entities.AddRange(
            new TestEntity { Id = 1, Name = "B" },
            new TestEntity { Id = 2, Name = "A" },
            new TestEntity { Id = 3, Name = "C" }
        );
        await db.SaveChangesAsync();

        var spec = new TestSpecification();
        spec.AddTestOrderBy(e => e.Name);

        // Act
        var result = db.Entities.ApplySpecs(spec).ToList();

        // Assert
        result[0].Name.ShouldBe("A");
        result[1].Name.ShouldBe("B");
        result[2].Name.ShouldBe("C");
    }

    [Fact]
    public async Task ApplySpecs_WithDescendingOrdering_ShouldApplyOrderDesc()
    {
        // Arrange
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options;
        using var db = new TestDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Entities.AddRange(
            new TestEntity { Id = 1, Name = "A" },
            new TestEntity { Id = 2, Name = "B" },
            new TestEntity { Id = 3, Name = "C" }
        );
        await db.SaveChangesAsync();

        var spec = new TestSpecification();
        spec.AddTestOrderByDescending(e => e.Name);

        // Act
        var result = db.Entities.ApplySpecs(spec).ToList();

        // Assert
        result[0].Name.ShouldBe("C");
        result[1].Name.ShouldBe("B");
        result[2].Name.ShouldBe("A");
    }

    [Fact]
    public async Task ApplySpecs_WithMixedOrdering_ShouldApplyInOrder()
    {
        // Arrange
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options;
        using var db = new TestDbContext(options);
        await db.Database.EnsureCreatedAsync();
        
        db.Categories.AddRange(
            new TestCategory { Id = 1, Name = "Cat1" },
            new TestCategory { Id = 2, Name = "Cat2" }
        );
        db.Entities.AddRange(
            new TestEntity { Id = 1, Name = "A", CategoryId = 1 },
            new TestEntity { Id = 2, Name = "A", CategoryId = 2 },
            new TestEntity { Id = 3, Name = "B", CategoryId = 1 }
        );
        await db.SaveChangesAsync();

        var spec = new TestSpecification();
        spec.AddTestOrderBy(e => e.Name);
        spec.AddTestOrderByDescending(e => e.CategoryId);

        // Act
        var result = db.Entities.ApplySpecs(spec).ToList();

        // Assert
        // A-2, A-1, B-1
        result[0].CategoryId.ShouldBe(2);
        result[1].CategoryId.ShouldBe(1);
        result[2].Name.ShouldBe("B");
    }

    [Fact]
    public async Task ApplySpecs_WithIgnoreQueryFilters_ShouldCallIgnoreQueryFilters()
    {
        // Arrange
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options;
        using var db = new TestDbContext(options);
        await db.Database.EnsureCreatedAsync();
        
        var spec = new TestSpecification();
        spec.TestIgnoreQueryFilters();

        // Act
        var result = db.Entities.ApplySpecs(spec).ToList();

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void EnsureSpecHasOrdering_WithOrdering_ShouldNotThrow()
    {
        // Arrange
        var spec = new TestSpecification();
        spec.AddTestOrderBy(e => e.Name);

        // Act & Assert
        Should.NotThrow(() => spec.EnsureSpecHasOrdering());
    }

    [Fact]
    public void EnsureSpecHasOrdering_WithoutOrdering_ShouldThrowNotSupportedException()
    {
        // Arrange
        var spec = new TestSpecification();

        // Act & Assert
        Should.Throw<NotSupportedException>(() => spec.EnsureSpecHasOrdering());
    }

    #endregion
}
