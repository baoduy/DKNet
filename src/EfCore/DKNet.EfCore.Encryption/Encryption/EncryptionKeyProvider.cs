namespace DKNet.EfCore.Encryption.Encryption;

/// <summary>
///     Provides encryption keys for different entity types.
/// </summary>
public interface IEncryptionKeyProvider
{
    #region Methods

    /// <summary>
    ///     Gets the encryption key for the specified entity type.
    /// </summary>
    /// <param name="entityType">The entity type for which to retrieve the encryption key.</param>
    /// <returns>The encryption key as a byte array.</returns>
    byte[] GetKey(Type entityType);

    #endregion
}