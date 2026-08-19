namespace EfCore.DtoGenerator.TestEntities.Share;

public interface IMetaDataEntity
{
    #region Properties

    public IDictionary<string, string> MetaData { get; }

    #endregion
}