namespace SlimBus.Domains.Share;

public abstract class AggregateRoot : EntityBase<Guid>
{
    #region Constructors

    protected AggregateRoot(string createdBy, DateTimeOffset? createdOn = null)
        : this(Guid.NewGuid(), createdBy, createdOn)
    {
    }

    protected AggregateRoot(Guid id, string createdBy, DateTimeOffset? createdOn = null)
        : base(id, createdBy, createdOn)
    {
    }

    /// <inheritdoc />
    protected AggregateRoot()
    {
    }

    #endregion Constructors
}