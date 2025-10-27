namespace DKNet.EfCore.DtoEntities.Share;

public interface IMetaDataEntity
{
    #region Properties

    public IDictionary<string, string> MetaData { get; }

    #endregion
}