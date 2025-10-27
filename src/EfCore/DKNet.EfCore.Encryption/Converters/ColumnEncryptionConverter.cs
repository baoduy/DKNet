using DKNet.EfCore.Encryption.Interfaces;

namespace DKNet.EfCore.Encryption.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public sealed class ColumnEncryptionConverter(IColumnEncryptionProvider encryptionProvider) : ValueConverter<string?, string?>(
    v => encryptionProvider.Encrypt(v),
    v => encryptionProvider.Decrypt(v));