using System.ComponentModel.DataAnnotations.Schema;
using DKNet.EfCore.Encryption.Attributes;
using DKNet.EfCore.Encryption.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Shouldly;

namespace EfCore.Encryption.Tests;

// Complex types
public class CardInfo
{
    #region Properties

    [Encrypted] public string Number { get; set; } = string.Empty;

    #endregion
}

public class WalletInfo
{
    #region Properties

    public CardInfo Card { get; set; } = new();

    public string Label { get; set; } = string.Empty;

    #endregion
}

public class PinInfo
{
    #region Properties

    [Encrypted] public int Pin { get; set; }

    #endregion
}

public class PlainAddress
{
    #region Properties

    public string City { get; set; } = string.Empty;

    #endregion
}

public class CustomerWithComplexCard
{
    #region Properties

    public CardInfo Card { get; set; } = new();

    public int Id { get; set; }

    #endregion
}

public class CustomerWithComplexPin
{
    #region Properties

    public int Id { get; set; }

    public PinInfo PinInfo { get; set; } = new();

    #endregion
}

public class CustomerWithNestedComplexCard
{
    #region Properties

    public int Id { get; set; }

    public WalletInfo Wallet { get; set; } = new();

    #endregion
}

public class CustomerWithComplexCardCollection
{
    #region Properties

    public List<CardInfo> Cards { get; set; } = [];

    public int Id { get; set; }

    #endregion
}

public class CustomerWithEncryptedComplexProperty
{
    #region Properties

    [Encrypted] public PlainAddress Address { get; set; } = new();

    public int Id { get; set; }

    #endregion
}

public class CustomerWithPlainComplexAddress
{
    #region Properties

    public PlainAddress Address { get; set; } = new();

    public int Id { get; set; }

    [Encrypted] public string? Secret { get; set; }

    #endregion
}

// Non-string members
public enum CardTier
{
    Basic,
    Gold
}

public class EncryptedIntEntity
{
    #region Properties

    public int Id { get; set; }

    [Encrypted] public int Pin { get; set; }

    #endregion
}

public class EncryptedNullableIntEntity
{
    #region Properties

    public int Id { get; set; }

    [Encrypted] public int? Pin { get; set; }

    #endregion
}

public class EncryptedBytesEntity
{
    #region Properties

    [Encrypted] public byte[] Blob { get; set; } = [];

    public int Id { get; set; }

    #endregion
}

public class EncryptedEnumEntity
{
    #region Properties

    public int Id { get; set; }

    [Encrypted] public CardTier Tier { get; set; }

    #endregion
}

public class EncryptedReferenceNavigationEntity
{
    #region Properties

    public int Id { get; set; }

    [Encrypted] public ParentKeyEntity? Parent { get; set; }

    #endregion
}

public class OwnedContact
{
    #region Properties

    [Encrypted] public string? Phone { get; set; }

    #endregion
}

public class EncryptedOwnedNavigationEntity
{
    #region Properties

    [Encrypted] public OwnedContact Contact { get; set; } = new();

    public int Id { get; set; }

    #endregion
}

public class TagEntity
{
    #region Properties

    public int Id { get; set; }

    public List<EncryptedSkipNavigationEntity> Owners { get; set; } = [];

    #endregion
}

public class EncryptedSkipNavigationEntity
{
    #region Properties

    public int Id { get; set; }

    [Encrypted] public List<TagEntity> Tags { get; set; } = [];

    #endregion
}

public class OwnerWithEncryptedOwnedString
{
    #region Properties

    public OwnedContact Contact { get; set; } = new();

    public int Id { get; set; }

    #endregion
}

public class NotMappedEncryptedIntEntity
{
    #region Properties

    public int Id { get; set; }

    [Encrypted][NotMapped] public int Pin { get; set; }

    #endregion
}

public class ModelBuilderExtensionsUnsupportedPlacementTests
{
    #region Methods

    // A convention-built model, so complex-type members and navigations are discovered as in a real DbContext.
    private static ModelBuilder CreateModelBuilder() => SqliteConventionSetBuilder.CreateModelBuilder();

    private static void ShouldRefuse(ModelBuilder modelBuilder, string typeName, string propertyName, string reason)
    {
        var ex = Should.Throw<InvalidOperationException>(() =>
            modelBuilder.UseColumnEncryption(new TestKeyProvider()));
        ex.Message.ShouldContain($"'{typeName}.{propertyName}'");
        ex.Message.ShouldContain(reason);
    }

    [Fact]
    public void UseColumnEncryption_WithEncryptedStringInComplexType_ShouldThrowInvalidOperationException()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<CustomerWithComplexCard>().ComplexProperty(c => c.Card);

        ShouldRefuse(modelBuilder, nameof(CardInfo), nameof(CardInfo.Number), "complex type");
    }

    [Fact]
    public void UseColumnEncryption_WithEncryptedStringInNestedComplexType_ShouldThrowInvalidOperationException()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<CustomerWithNestedComplexCard>()
            .ComplexProperty(c => c.Wallet, w => w.ComplexProperty(x => x.Card));

        ShouldRefuse(modelBuilder, nameof(CardInfo), nameof(CardInfo.Number), "complex type");
    }

    [Fact]
    public void UseColumnEncryption_WithEncryptedStringInJsonComplexType_ShouldThrowInvalidOperationException()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<CustomerWithComplexCard>().ComplexProperty(c => c.Card, b => b.ToJson());

        ShouldRefuse(modelBuilder, nameof(CardInfo), nameof(CardInfo.Number), "complex type");
    }

    [Fact]
    public void UseColumnEncryption_WithEncryptedStringInComplexCollection_ShouldThrowInvalidOperationException()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<CustomerWithComplexCardCollection>().ComplexCollection(c => c.Cards, b => b.ToJson());

        ShouldRefuse(modelBuilder, nameof(CardInfo), nameof(CardInfo.Number), "complex type");
    }

    [Fact]
    public void UseColumnEncryption_WithEncryptedIntInComplexType_ShouldThrowComplexTypeRefusal()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<CustomerWithComplexPin>().ComplexProperty(c => c.PinInfo);

        ShouldRefuse(modelBuilder, nameof(PinInfo), nameof(PinInfo.Pin), "complex type");
    }

    [Fact]
    public void UseColumnEncryption_WithEncryptedComplexProperty_ShouldThrowInvalidOperationException()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<CustomerWithEncryptedComplexProperty>().ComplexProperty(c => c.Address);

        ShouldRefuse(modelBuilder, nameof(CustomerWithEncryptedComplexProperty),
            nameof(CustomerWithEncryptedComplexProperty.Address), "complex type");
    }

    [Fact]
    public void UseColumnEncryption_WithEncryptedInt_ShouldThrowInvalidOperationException()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<EncryptedIntEntity>();

        ShouldRefuse(modelBuilder, nameof(EncryptedIntEntity), nameof(EncryptedIntEntity.Pin), "string");
    }

    [Fact]
    public void UseColumnEncryption_WithEncryptedNullableInt_ShouldThrowInvalidOperationException()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<EncryptedNullableIntEntity>();

        ShouldRefuse(modelBuilder, nameof(EncryptedNullableIntEntity), nameof(EncryptedNullableIntEntity.Pin),
            "string");
    }

    [Fact]
    public void UseColumnEncryption_WithEncryptedByteArray_ShouldThrowInvalidOperationException()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<EncryptedBytesEntity>();

        ShouldRefuse(modelBuilder, nameof(EncryptedBytesEntity), nameof(EncryptedBytesEntity.Blob), "string");
    }

    [Fact]
    public void UseColumnEncryption_WithEncryptedEnumConvertedToString_ShouldThrowInvalidOperationException()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<EncryptedEnumEntity>().Property(e => e.Tier).HasConversion<string>();

        ShouldRefuse(modelBuilder, nameof(EncryptedEnumEntity), nameof(EncryptedEnumEntity.Tier), "string");
    }

    [Fact]
    public void UseColumnEncryption_WithEncryptedEnum_ShouldThrowInvalidOperationException()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<EncryptedEnumEntity>();

        ShouldRefuse(modelBuilder, nameof(EncryptedEnumEntity), nameof(EncryptedEnumEntity.Tier), "string");
    }

    [Fact]
    public void UseColumnEncryption_WithEncryptedReferenceNavigation_ShouldThrowInvalidOperationException()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<EncryptedReferenceNavigationEntity>();

        ShouldRefuse(modelBuilder, nameof(EncryptedReferenceNavigationEntity),
            nameof(EncryptedReferenceNavigationEntity.Parent), "string");
    }

    [Fact]
    public void UseColumnEncryption_WithEncryptedOwnedNavigation_ShouldThrowInvalidOperationException()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<EncryptedOwnedNavigationEntity>().OwnsOne(e => e.Contact);

        ShouldRefuse(modelBuilder, nameof(EncryptedOwnedNavigationEntity),
            nameof(EncryptedOwnedNavigationEntity.Contact), "string");
    }

    [Fact]
    public void UseColumnEncryption_WithEncryptedSkipNavigation_ShouldThrowInvalidOperationException()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<EncryptedSkipNavigationEntity>().HasMany(e => e.Tags).WithMany(t => t.Owners);

        ShouldRefuse(modelBuilder, nameof(EncryptedSkipNavigationEntity),
            nameof(EncryptedSkipNavigationEntity.Tags), "string");
    }

    [Fact]
    public void UseColumnEncryption_WithEncryptedStringOnOwnedType_ShouldApplyConverterWithOwnedTypeKey()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<OwnerWithEncryptedOwnedString>().OwnsOne(e => e.Contact);
        var keyProvider = new TestKeyProvider();

        modelBuilder.UseColumnEncryption(keyProvider);

        var phone = modelBuilder.Model.FindEntityType(typeof(OwnedContact))!.FindProperty(nameof(OwnedContact.Phone))!;
        phone.GetValueConverter().ShouldNotBeNull();
        keyProvider.RequestedTypes.ShouldBe([typeof(OwnedContact)]);
    }

    [Fact]
    public void UseColumnEncryption_WithEncryptedStringOnJsonOwnedType_ShouldApplyConverter()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<OwnerWithEncryptedOwnedString>().OwnsOne(e => e.Contact, b => b.ToJson());

        modelBuilder.UseColumnEncryption(new TestKeyProvider());

        modelBuilder.Model.FindEntityType(typeof(OwnedContact))!.FindProperty(nameof(OwnedContact.Phone))!
            .GetValueConverter().ShouldNotBeNull();
    }

    [Fact]
    public void UseColumnEncryption_WithComplexTypeWithoutEncrypted_ShouldEncryptOnlyTheMarkedEntityString()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<CustomerWithPlainComplexAddress>().ComplexProperty(c => c.Address);

        modelBuilder.UseColumnEncryption(new TestKeyProvider());

        var entityType = modelBuilder.Model.FindEntityType(typeof(CustomerWithPlainComplexAddress))!;
        entityType.FindProperty(nameof(CustomerWithPlainComplexAddress.Secret))!.GetValueConverter()
            .ShouldNotBeNull();
        entityType.FindComplexProperty(nameof(CustomerWithPlainComplexAddress.Address))!.ComplexType
            .FindProperty(nameof(PlainAddress.City))!.GetValueConverter().ShouldBeNull();
    }

    [Fact]
    public void UseColumnEncryption_WithNotMappedEncryptedInt_ShouldNotThrow()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<NotMappedEncryptedIntEntity>();

        Should.NotThrow(() => modelBuilder.UseColumnEncryption(new TestKeyProvider()));
        modelBuilder.Model.FindEntityType(typeof(NotMappedEncryptedIntEntity))!
            .FindProperty(nameof(NotMappedEncryptedIntEntity.Pin)).ShouldBeNull();
    }

    [Fact]
    public void UseColumnEncryption_WithIgnoredEncryptedInt_ShouldNotThrow()
    {
        var modelBuilder = CreateModelBuilder();
        modelBuilder.Entity<EncryptedIntEntity>().Ignore(e => e.Pin);

        Should.NotThrow(() => modelBuilder.UseColumnEncryption(new TestKeyProvider()));
        modelBuilder.Model.FindEntityType(typeof(EncryptedIntEntity))!
            .FindProperty(nameof(EncryptedIntEntity.Pin)).ShouldBeNull();
    }

    #endregion
}
