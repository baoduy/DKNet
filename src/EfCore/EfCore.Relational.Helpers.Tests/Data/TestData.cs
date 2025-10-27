using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EfCore.Relational.Helpers.Tests.Data;

[Table(nameof(TestEntity))]
public class TestEntity
{
    #region Properties

    [Key] public int Id { get; set; }

    [StringLength(1000)] public string Name { get; set; } = null!;

    #endregion
}

[Table(nameof(NotMappedTestEntity))]
public class NotMappedTestEntity
{
    #region Properties

    [Key] public int Id { get; set; }

    public string Name { get; set; } = null!;

    #endregion
}

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    #region Methods

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<TestEntity>();
    }

    #endregion
}