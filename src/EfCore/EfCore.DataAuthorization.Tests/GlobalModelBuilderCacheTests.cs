using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Extensions.Configurations;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;

namespace EfCore.DataAuthorization.Tests;

/// <summary>
///     DRK-1970 (root-cause shape B): a model cached by a hand-built context must not hide a builder that a later
///     DI context of the same options shape registers through <c>AddGlobalModelBuilder&lt;T&gt;()</c>. The probe,
///     context and entity are owned by this test; the options scan <c>DKNet.EfCore.Abstractions</c>, which holds no
///     <see cref="IGlobalModelBuilder" />, so the probe reaches the model only through the global registration.
/// </summary>
public class GlobalModelBuilderCacheTests
{
    #region Methods

    [Fact]
    public void S2_HandBuiltContextFirst_DiContextWithSameShape_AppliesBuilderRegisteredThroughServices()
    {
        // Arrange
        var assembly = typeof(IAuditedProperties).Assembly;
        var handBuiltOptions = new DbContextOptionsBuilder<S2ProbeContext>()
            .UseInMemoryDatabase(nameof(S2ProbeContext))
            .UseAutoConfigModel(assembly)
            .Options;

        using (var handBuilt = new S2ProbeContext(handBuiltOptions))
            handBuilt.Model.FindEntityType(typeof(S2ProbeEntity))!.GetDeclaredQueryFilters().ShouldBeEmpty();

        // Act
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddGlobalModelBuilder<S2ProbeBuilder>()
            .AddDbContextWithHook<S2ProbeContext>(builder =>
                builder.UseInMemoryDatabase(nameof(S2ProbeContext))
                    .UseAutoConfigModel(assembly))
            .BuildServiceProvider();
        using var scope = provider.CreateScope();
        var diContext = scope.ServiceProvider.GetRequiredService<S2ProbeContext>();

        // Assert
        diContext.Model.FindEntityType(typeof(S2ProbeEntity))!.GetDeclaredQueryFilters()
            .Select(f => f.Key).ShouldBe(["DRK1970.S2Probe"]);
    }

    #endregion

    public sealed class S2ProbeEntity
    {
        #region Properties

        public int Id { get; set; }

        #endregion
    }

    public sealed class S2ProbeContext(DbContextOptions<S2ProbeContext> options) : DbContext(options)
    {
        #region Properties

        public DbSet<S2ProbeEntity> Entities => Set<S2ProbeEntity>();

        #endregion
    }

    // Registered process-wide and scanned with this test assembly by other contexts: applies to its own context only.
    public sealed class S2ProbeBuilder : IGlobalModelBuilder
    {
        #region Methods

        public void Apply(ModelBuilder modelBuilder, DbContext context)
        {
            if (context is not S2ProbeContext) return;
            modelBuilder.Entity<S2ProbeEntity>().HasQueryFilter("DRK1970.S2Probe", e => e.Id > 0);
        }

        #endregion
    }
}
