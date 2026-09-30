using DKNet.EfCore.DataAuthorization.Internals;
using Microsoft.EntityFrameworkCore;

namespace EfCore.DataAuthorization.Tests.TestEntities;

/// <summary>DRK-1897 shape (b): a TPH root that is NOT <see cref="IOwnedBy" />.</summary>
public class UnownedDocument(string title)
{
    #region Properties

    public Guid Id { get; private set; }

    public string Title { get; private set; } = title;

    #endregion
}

/// <summary>DRK-1897 shape (b): an <see cref="IOwnedBy" /> type derived from the non-owned root.</summary>
public class OwnedInvoiceDocument(string title, string ownedBy) : UnownedDocument(title), IOwnedBy
{
    #region Properties

    public string OwnedBy { get; private set; } = ownedBy;

    #endregion
}

/// <summary>
///     An isolated context for DRK-1897 shape (b). Its hierarchy is configured only here in
///     <see cref="OnModelCreating" />, never as an <c>IEntityTypeConfiguration</c>, so the
///     <c>UseAutoConfigModel()</c> scan of this test assembly never adds it to <see cref="DddContext" />.
///     Implements <see cref="IDataOwnerDbContext" /> so the DRK-898 fail-closed guard cannot be what throws.
///     Applies <see cref="DataOwnerAuthQuery" /> directly, as <see cref="NonOwnerDbContext" /> does.
/// </summary>
public sealed class UnownedRootHierarchyDbContext(DbContextOptions<UnownedRootHierarchyDbContext> options)
    : DbContext(options), IDataOwnerDbContext
{
    #region Properties

    public IEnumerable<string> AccessibleKeys => ["Steven"];

    public bool IsUnrestrictedAccess => false;

    #endregion

    #region Methods

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<UnownedDocument>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Title).HasMaxLength(100);
        });
        modelBuilder.Entity<OwnedInvoiceDocument>().HasBaseType<UnownedDocument>();
        new DataOwnerAuthQuery().Apply(modelBuilder, this);
    }

    #endregion
}
