namespace EfCore.DtoGenerator.TestEntities.Share;

[Owned]
public sealed class LoginInfo
{
    #region Properties

    public DateTimeOffset CreatedOn { get; init; } = DateTimeOffset.Now;

    [MaxLength(50)] public Guid Id { get; init; }

    [MaxLength(100)] public required string Email { get; init; }

    #endregion
}