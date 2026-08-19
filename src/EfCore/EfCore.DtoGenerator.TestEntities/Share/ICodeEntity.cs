namespace EfCore.DtoGenerator.TestEntities.Share;

public interface ICodeEntity
{
    #region Properties

    [MaxLength(100)] string Code { get; }

    #endregion
}