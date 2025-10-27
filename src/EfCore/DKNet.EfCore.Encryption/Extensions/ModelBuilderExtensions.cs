using DKNet.EfCore.Encryption.Attributes;
using DKNet.EfCore.Encryption.Converters;
using DKNet.EfCore.Encryption.Encryption;
using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.Encryption.Extensions;

public static class ModelBuilderExtensions
{
    public static void UseColumnEncryption(this ModelBuilder modelBuilder, IEncryptionKeyProvider encryptionKeyProvider)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(encryptionKeyProvider);

        var stringProperties = modelBuilder.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p =>
                p.ClrType == typeof(string) &&
                !p.IsPrimaryKey() &&
                !p.IsForeignKey());

        foreach (var property in stringProperties)
        {
            var propertyInfo = property.PropertyInfo;
            if (propertyInfo == null || !Attribute.IsDefined(propertyInfo, typeof(EncryptedAttribute))) continue;

            var key = encryptionKeyProvider.GetKey(propertyInfo.DeclaringType!);
            var converter = new ColumnEncryptionConverter(new AesGcmColumnEncryptionProvider(key));
            property.SetValueConverter(converter);
        }
    }
}