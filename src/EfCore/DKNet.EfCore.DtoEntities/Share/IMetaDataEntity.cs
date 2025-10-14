namespace DKNet.EfCore.DtoEntities.Share;

public interface IMetaDataEntity
{
    public IDictionary<string, string> MetaData { get; }
}