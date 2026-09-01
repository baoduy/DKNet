using DKNet.EfCore.DataAuthorization.Internals;
using Microsoft.EntityFrameworkCore;

namespace EfCore.DataAuthorization.Tests.TestEntities;

/// <summary>
///     A DbContext with an <see cref="IOwnedBy" /> entity (<see cref="Root" />) that does NOT implement
///     <see cref="IDataOwnerDbContext" /> — the exact misconfiguration DRK-898's fail-closed guard exists to
///     catch. Applies <see cref="DataOwnerAuthQuery" /> directly instead of through
///     <c>AddDataOwnerProvider&lt;TDbContext, TProvider&gt;()</c>, whose tightened generic constraint would make
///     this class impossible to express via the DI setup extension, so the runtime guard can still be exercised.
/// </summary>
public sealed class NonOwnerDbContext(DbContextOptions<NonOwnerDbContext> options) : DbContext(options)
{
    #region Properties

    public DbSet<Root> Roots => Set<Root>();

    #endregion

    #region Methods

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Root>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(100);
        });
        new DataOwnerAuthQuery().Apply(modelBuilder, this);
    }

    #endregion
}
