namespace DKNet.EfCore.DtoEntities.Share;

public abstract class AggregateRoot : DomainEntity
{
    #region Constructors

    protected AggregateRoot(string byUser) : base(byUser)
    {
    }

    protected AggregateRoot(Guid id, string createdBy) : base(id, createdBy)
    {
    }

    #endregion
}