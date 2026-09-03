using System.Linq.Expressions;
using System.Reflection;
using DKNet.EfCore.Extensions.Configurations;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfCore.Extensions.Tests;

/// <summary>
///     Covers <see cref="GlobalQueryFilter.Apply" />'s reflection-invoke unwrap (DRK-898): an exception thrown
///     from <see cref="GlobalQueryFilter.HasQueryFilter{TEntity}" /> must reach the caller as itself, not
///     wrapped in a <see cref="TargetInvocationException" />. <c>DKNet.EfCore.DataAuthorization</c>'s
///     fail-closed guard depends on this unwrap to surface <see cref="InvalidOperationException" /> — this test
///     covers the shared base class directly so any <see cref="GlobalQueryFilter" /> implementation, not only
///     that one, keeps the guarantee.
/// </summary>
public class GlobalQueryFilterTests
{
    #region Methods

    [Fact]
    public void Apply_WhenHasQueryFilterThrows_SurfacesOriginalExceptionNotTargetInvocationException()
    {
        // Arrange: DbContext.Model lazily triggers OnModelCreating, which calls ThrowingFilter.Apply
        using var context = new ThrowingFilterDbContext(
            new DbContextOptionsBuilder<ThrowingFilterDbContext>().UseSqlite("Data Source=:memory:").Options);

        // Act
        var ex = Record.Exception(() => context.Model);

        // Assert
        ex.ShouldNotBeNull();
        ex.ShouldBeOfType<InvalidOperationException>();
        ex.Message.ShouldBe(ThrowingFilter.FailureMessage);
    }

    [Fact]
    public void Apply_WhenHasQueryFilterReturnsExpression_AppliesFilterToModel()
    {
        // Arrange
        using var context = new PassingFilterDbContext(
            new DbContextOptionsBuilder<PassingFilterDbContext>().UseSqlite("Data Source=:memory:").Options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(FilterableEntity));

        // Assert: the filter was actually registered on the model, not merely "didn't throw"
        entityType.ShouldNotBeNull();
        entityType!.GetDeclaredQueryFilters().ShouldNotBeNull();
    }

    [Fact]
    public void IgnorableFilterKeys_AfterApply_ReturnsCachedArrayContainingFilterKey()
    {
        // Arrange: DbContext.Model lazily triggers OnModelCreating, which calls PassingFilter.Apply
        using var context = new PassingFilterDbContext(
            new DbContextOptionsBuilder<PassingFilterDbContext>().UseSqlite("Data Source=:memory:").Options);
        _ = context.Model;

        // Act: read the property twice
        var first = GlobalQueryFilter.IgnorableFilterKeys;
        var second = GlobalQueryFilter.IgnorableFilterKeys;

        // Assert: the newly-applied filter is reflected, and both reads return the same cached
        // array instance rather than a freshly rebuilt collection on every access (P21).
        first.ShouldContain(nameof(PassingFilter));
        ReferenceEquals(first, second).ShouldBeTrue();
    }

    #endregion
}

internal interface IFilterable
{
    bool IsHidden { get; }
}

internal sealed class FilterableEntity : IFilterable
{
    #region Properties

    public int Id { get; init; }
    public bool IsHidden { get; init; }

    #endregion
}

internal sealed class ThrowingFilter : GlobalQueryFilter
{
    #region Fields

    public const string FailureMessage = "boom from HasQueryFilter";

    #endregion

    #region Properties

    public override string FilterKey => nameof(ThrowingFilter);

    #endregion

    #region Methods

    protected override IEnumerable<IMutableEntityType> GetEntityTypes(ModelBuilder modelBuilder) =>
        modelBuilder.Model.GetEntityTypes().Where(t => t.ClrType == typeof(FilterableEntity));

    protected override Expression<Func<TEntity, bool>>? HasQueryFilter<TEntity>(DbContext context) =>
        throw new InvalidOperationException(FailureMessage);

    #endregion
}

internal sealed class PassingFilter : GlobalQueryFilter
{
    #region Properties

    public override string FilterKey => nameof(PassingFilter);

    #endregion

    #region Methods

    protected override IEnumerable<IMutableEntityType> GetEntityTypes(ModelBuilder modelBuilder) =>
        modelBuilder.Model.GetEntityTypes().Where(t => t.ClrType == typeof(FilterableEntity));

    protected override Expression<Func<TEntity, bool>>? HasQueryFilter<TEntity>(DbContext context) =>
        x => !((IFilterable)x).IsHidden;

    #endregion
}

internal sealed class ThrowingFilterDbContext(DbContextOptions<ThrowingFilterDbContext> options) : DbContext(options)
{
    #region Properties

    public DbSet<FilterableEntity> Items => Set<FilterableEntity>();

    #endregion

    #region Methods

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<FilterableEntity>().HasKey(e => e.Id);
        new ThrowingFilter().Apply(modelBuilder, this);
    }

    #endregion
}

internal sealed class PassingFilterDbContext(DbContextOptions<PassingFilterDbContext> options) : DbContext(options)
{
    #region Properties

    public DbSet<FilterableEntity> Items => Set<FilterableEntity>();

    #endregion

    #region Methods

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<FilterableEntity>().HasKey(e => e.Id);
        new PassingFilter().Apply(modelBuilder, this);
    }

    #endregion
}
