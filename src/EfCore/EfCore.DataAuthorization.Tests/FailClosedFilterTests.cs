using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace EfCore.DataAuthorization.Tests;

/// <summary>
///     Regression coverage for DRK-898: a DbContext with an <see cref="IOwnedBy" /> entity that does not
///     implement <see cref="IDataOwnerDbContext" /> must fail closed at model-build time instead of silently
///     applying no filter, which would expose every owner's rows. Unlike
///     <see cref="DataAuthorizationFilterTests" />, this drives the guard through
///     <see cref="DKNet.EfCore.Extensions.Configurations.GlobalQueryFilter.Apply" /> — the real invocation path,
///     via reflection — so a regression of its <see cref="TargetInvocationException" /> unwrap would also fail
///     this test, not just a direct-call test.
/// </summary>
public class FailClosedFilterTests
{
    #region Methods

    [Fact]
    public void ModelBuild_WithNonIDataOwnerDbContext_ThrowsInvalidOperationExceptionNotTargetInvocationException()
    {
        // Arrange: DbContext.Model lazily triggers OnModelCreating, which applies DataOwnerAuthQuery
        // against a context that does not implement IDataOwnerDbContext.
        using var context = new NonOwnerDbContext(
            new DbContextOptionsBuilder<NonOwnerDbContext>().UseSqlite("Data Source=:memory:").Options);

        // Act
        var ex = Record.Exception(() => context.Model);

        // Assert: surfaces as the real exception, not wrapped by GlobalQueryFilter.Apply's reflection Invoke
        ex.ShouldNotBeNull();
        ex.ShouldBeOfType<InvalidOperationException>();
        ex.Message.ShouldContain(nameof(IDataOwnerDbContext));
        ex.Message.ShouldContain(nameof(NonOwnerDbContext));
    }

    #endregion
}
