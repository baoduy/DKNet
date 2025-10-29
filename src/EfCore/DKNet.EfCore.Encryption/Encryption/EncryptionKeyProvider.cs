namespace DKNet.EfCore.Encryption.Encryption;

public interface IEncryptionKeyProvider
{
    #region Methods

    byte[] GetKey(Type entityType);

    #endregion
}

public abstract class EncryptionKeyProvider : IEncryptionKeyProvider
{
    #region Methods

    public abstract byte[] GetKey(Type entityType);

    #endregion
}