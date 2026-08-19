namespace EfCore.DtoGenerator.TestEntities.Share;

[Owned]
public sealed record ClientAppInfo
{
    #region Properties

    public DateTimeOffset CreatedOn { get; init; }

    [MaxLength(50)] public required Guid ClientId { get; init; }

    #endregion
}