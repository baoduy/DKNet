using DKNet.EfCore.Abstractions.Entities;

namespace AspCore.Extensions.Tests.TestEntities;

/// <summary>
///     Audited, Guid-keyed entity used to exercise <c>MapGetList</c>'s default recent-activity window (DRK-1164
///     §5, "Default recent-activity window for audited records"). Exposes a filterable <see cref="Status" /> so
///     the "window combines with the caller's other conditions" scenario is reachable.
/// </summary>
public sealed class OrderEntity : AuditedEntity
{
    #region Constructors

    public OrderEntity()
    {
    }

    public OrderEntity(Guid id, string reference, string status) : base(id)
    {
        Reference = reference;
        Status = status;
    }

    #endregion

    #region Properties

    public string Reference { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    #endregion

    #region Methods

    /// <summary>
    ///     Test-only seeding hook: <see cref="AuditedEntity{TKey}.SetCreatedBy" />/<see cref="AuditedEntity{TKey}.SetUpdatedBy" />
    ///     are <see langword="protected" />, so a fixture seeding rows at explicit instants needs a public way in.
    /// </summary>
    /// <param name="createdOn">The instant to seed <c>CreatedOn</c> at.</param>
    /// <param name="updatedOn">The instant to seed <c>UpdatedOn</c> at, or <see langword="null" /> to leave it unset.</param>
    public void Seed(DateTimeOffset createdOn, DateTimeOffset? updatedOn = null)
    {
        SetCreatedBy("seed", createdOn);
        if (updatedOn is not null) SetUpdatedBy("seed", updatedOn);
    }

    #endregion
}

/// <summary>Projection model <see cref="OrderEntity" /> maps to by convention (matching property names).</summary>
public sealed class OrderModel
{
    #region Properties

    public Guid Id { get; init; }

    public string Reference { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset CreatedOn { get; init; }

    #endregion
}

/// <summary>
///     Guid-keyed entity implementing <see cref="IAuditedProperties" /> directly rather than through
///     <see cref="AuditedEntity" /> — a genuinely listed type that carries audit timestamps but sits outside the
///     narrower <see cref="IAuditedEntity{TKey}" /> set the existing newest-first default ordering recognises.
///     Proves the window applies on the strength of <see cref="IAuditedProperties" /> alone (DRK-1164 §5, "The
///     window covers every listed type that carries audit timestamps").
/// </summary>
public sealed class InvoiceEntity : Entity, IAuditedProperties
{
    #region Constructors

    public InvoiceEntity()
    {
    }

    public InvoiceEntity(Guid id, string number, DateTimeOffset createdOn, DateTimeOffset? updatedOn = null) : base(id)
    {
        Number = number;
        CreatedOn = createdOn;
        UpdatedOn = updatedOn;
    }

    #endregion

    #region Properties

    public string Number { get; set; } = string.Empty;

    public DateTimeOffset CreatedOn { get; set; }

    public DateTimeOffset? UpdatedOn { get; set; }

    public string CreatedBy { get; set; } = "seed";

    public string? UpdatedBy { get; set; }

    #endregion
}

/// <summary>Projection model <see cref="InvoiceEntity" /> maps to by convention (matching property names).</summary>
public sealed class InvoiceModel
{
    #region Properties

    public Guid Id { get; init; }

    public string Number { get; init; } = string.Empty;

    public DateTimeOffset CreatedOn { get; init; }

    #endregion
}
