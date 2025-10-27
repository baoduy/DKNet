namespace DKNet.EfCore.Encryption.Encryption;

public interface IEncryptionKeyProvider
{
    byte[] GetKey(Type entityType);
}

public abstract class EncryptionKeyProvider:IEncryptionKeyProvider
{
    public abstract byte[] GetKey(Type entityType);
}