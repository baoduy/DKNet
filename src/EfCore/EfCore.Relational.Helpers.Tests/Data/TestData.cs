using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EfCore.Relational.Helpers.Tests.Data;

[Table(nameof(TestEntity))]
public class TestEntity
{
    [Key] public int Id { get; set; }

    [StringLength(1000)] public string Name { get; set; } = null!;
}

[Table(nameof(NotMappedTestEntity))]
public class NotMappedTestEntity
{
    [Key] public int Id { get; set; }

    public string Name { get; set; } = null!;
}

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<TestEntity>();
    }
}