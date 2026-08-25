using DKNet.EfCore.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace EfCore.DataAuthorization.Tests.TestEntities;

/// <summary>
///     A minimal audited entity with no <see cref="IOwnedBy" /> marker, used to prove that automatic
///     modifier recording never fires for a consumer that has not opted into data authorization.
/// </summary>
public sealed class PlainAuditedEntity : AuditedEntity<Guid>
{
    #region Constructors

    public PlainAuditedEntity(string createdBy) : base(Guid.NewGuid()) => SetCreatedBy(createdBy);

    #endregion

    #region Properties

    public string Payload { get; private set; } = string.Empty;

    #endregion

    #region Methods

    public void Rename(string payload) => Payload = payload;

    #endregion
}

/// <summary>
///     A bare <see cref="DbContext" /> registered with none of <c>DKNet.EfCore.DataAuthorization</c>'s
///     setup — no <c>AddDataOwnerProvider</c> and no <c>AddDbContextWithHook</c> — representing a consumer
///     that has never opted into data authorization.
/// </summary>
public sealed class PlainDbContext(DbContextOptions<PlainDbContext> options) : DbContext(options)
{
    #region Properties

    public DbSet<PlainAuditedEntity> Items => Set<PlainAuditedEntity>();

    #endregion
}
