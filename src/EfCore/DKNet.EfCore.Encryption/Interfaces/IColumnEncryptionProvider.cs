namespace DKNet.EfCore.Encryption.Interfaces;

public interface IColumnEncryptionProvider
{
    string? Encrypt(string? plaintext);
    string? Decrypt(string? ciphertext);
}