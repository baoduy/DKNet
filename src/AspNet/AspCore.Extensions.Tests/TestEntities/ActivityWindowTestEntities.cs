using DKNet.EfCore.Abstractions.Entities;

namespace AspCore.Extensions.Tests.TestEntities;

/// <summary>
///     Audited, Guid-keyed entity used to exercise <c>MapGetList</c>'s default recent-activity window
///     (DRK-1166). <see cref="Status" /> lets a scenario prove the window ANDs with a caller's own filter
///     rather than replacing it.
/// </summary>
public sealed class OrderEntity : AuditedEntity
{
    #region Constructors

    public OrderEntity()
    {
    }

    public OrderEntity(Guid id, DateTimeOffset createdOn, DateTimeOffset? updatedOn = null, string status = "Open")
        : base(id)
    {
        Status = status;
        SetCreatedBy("seed", createdOn);
        if (updatedOn is not null) SetUpdatedBy("seed", updatedOn);
    }

    #endregion

    #region Properties

    public string Status { get; set; } = "Open";

    #endregion
}

/// <summary>Projection model <see cref="OrderEntity" /> maps to by convention (matching property names).</summary>
public sealed class OrderModel
{
    #region Properties

    public Guid Id { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset CreatedOn { get; init; }

    public DateTimeOffset? UpdatedOn { get; init; }

    #endregion
}

/// <summary>
///     A listed type that carries audit timestamps by implementing <see cref="IAuditedProperties" />
///     <b>directly</b>, without going through <see cref="IAuditedEntity{TKey}" /> or <see cref="AuditedEntity" />
///     — proving the activity window recognises R6's wider rule
///     (<c>typeof(IAuditedProperties).IsAssignableFrom(...)</c>) rather than the ordering default's narrower
///     <c>typeof(IAuditedEntity&lt;TKey&gt;).IsAssignableFrom(...)</c> check.
/// </summary>
public sealed class TicketEntity : IEntity<Guid>, IAuditedProperties
{
    #region Properties

    public Guid Id { get; init; }

    public string CreatedBy { get; init; } = "seed";

    public DateTimeOffset CreatedOn { get; init; }

    public string? UpdatedBy { get; init; }

    public DateTimeOffset? UpdatedOn { get; init; }

    #endregion
}

/// <summary>Projection model <see cref="TicketEntity" /> maps to by convention (matching property names).</summary>
public sealed class TicketModel
{
    #region Properties

    public Guid Id { get; init; }

    public DateTimeOffset CreatedOn { get; init; }

    #endregion
}
