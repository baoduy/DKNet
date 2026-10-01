using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Extensions.Configurations;
using Microsoft.Extensions.DependencyInjection;

namespace EfCore.Extensions.Tests;

/// <summary>
///     Runs alone: <c>AddGlobalModelBuilder</c> is process-wide, so a type registered by a parallel test between
///     S3's two contexts would legitimately change the registered set and rebuild the model.
/// </summary>
[CollectionDefinition(nameof(GlobalModelBuilderCacheCollection), DisableParallelization = true)]
public sealed class GlobalModelBuilderCacheCollection;

/// <summary>
///     DRK-1970: EF Core's model cache must be aware of builders registered through
///     <c>AddGlobalModelBuilder&lt;T&gt;()</c>. Each scenario owns its probe builder, context and entity, and its
///     options scan <c>DKNet.EfCore.Abstractions</c>, which holds no <see cref="IGlobalModelBuilder" />, so the
///     probe reaches the model only through the global registration.
/// </summary>
[Collection(nameof(GlobalModelBuilderCacheCollection))]
public class GlobalModelBuilderCacheTests
{
    #region Methods

    [Fact]
    public void S1_SameOptions_BuilderRegisteredBetweenContexts_SecondModelAppliesBuilder()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<S1ProbeContext>()
            .UseInMemoryDatabase(nameof(S1ProbeContext))
            .UseAutoConfigModel(typeof(IAuditedProperties).Assembly)
            .Options;

        using var first = new S1ProbeContext(options);
        first.Model.FindEntityType(typeof(S1ProbeEntity))!.GetDeclaredQueryFilters().ShouldBeEmpty();

        // Act
        new ServiceCollection().AddGlobalModelBuilder<S1ProbeBuilder>();
        using var second = new S1ProbeContext(options);

        // Assert
        second.Model.FindEntityType(typeof(S1ProbeEntity))!.GetDeclaredQueryFilters()
            .Select(f => f.Key).ShouldBe(["DRK1970.S1Probe"]);
    }

    [Fact]
    public void S3_SameOptions_AlreadyRegisteredBuilderRegisteredAgain_ContextsShareSameModel()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<S3ProbeContext>()
            .UseInMemoryDatabase(nameof(S3ProbeContext))
            .UseAutoConfigModel(typeof(IAuditedProperties).Assembly)
            .Options;

        new ServiceCollection().AddGlobalModelBuilder<S3ProbeBuilder>();
        using var first = new S3ProbeContext(options);
        var firstModel = first.Model;
        firstModel.FindEntityType(typeof(S3ProbeEntity))!.GetDeclaredQueryFilters()
            .Select(f => f.Key).ShouldBe(["DRK1970.S3Probe"]);

        // Act
        new ServiceCollection().AddGlobalModelBuilder<S3ProbeBuilder>();
        using var second = new S3ProbeContext(options);

        // Assert
        second.Model.ShouldBeSameAs(firstModel);
    }

    #endregion

    public sealed class S1ProbeEntity
    {
        #region Properties

        public int Id { get; set; }

        #endregion
    }

    public sealed class S1ProbeContext(DbContextOptions<S1ProbeContext> options) : DbContext(options)
    {
        #region Properties

        public DbSet<S1ProbeEntity> Entities => Set<S1ProbeEntity>();

        #endregion
    }

    // Registered process-wide and scanned with this test assembly by other contexts: applies to its own context only.
    public sealed class S1ProbeBuilder : IGlobalModelBuilder
    {
        #region Methods

        public void Apply(ModelBuilder modelBuilder, DbContext context)
        {
            if (context is not S1ProbeContext) return;
            modelBuilder.Entity<S1ProbeEntity>().HasQueryFilter("DRK1970.S1Probe", e => e.Id > 0);
        }

        #endregion
    }

    public sealed class S3ProbeEntity
    {
        #region Properties

        public int Id { get; set; }

        #endregion
    }

    public sealed class S3ProbeContext(DbContextOptions<S3ProbeContext> options) : DbContext(options)
    {
        #region Properties

        public DbSet<S3ProbeEntity> Entities => Set<S3ProbeEntity>();

        #endregion
    }

    public sealed class S3ProbeBuilder : IGlobalModelBuilder
    {
        #region Methods

        public void Apply(ModelBuilder modelBuilder, DbContext context)
        {
            if (context is not S3ProbeContext) return;
            modelBuilder.Entity<S3ProbeEntity>().HasQueryFilter("DRK1970.S3Probe", e => e.Id > 0);
        }

        #endregion
    }
}
