using DKNet.EfCore.Encryption.Extensions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Shouldly;

namespace EfCore.Encryption.Tests;

// Pins the refusal text itself: EF Core's own converter type-mismatch error also names 'Type.Prop' and "string",
// so a fragment assertion alone cannot tell the package's refusal from EF's.
public class ModelBuilderExtensionsRefusalMessageTests
{
    #region Methods

    [Fact]
    public void UseColumnEncryption_WithEncryptedInt_ShouldRefuseBeforeAskingForAKey()
    {
        var modelBuilder = SqliteConventionSetBuilder.CreateModelBuilder();
        modelBuilder.Entity<EncryptedIntEntity>();
        var keyProvider = new TestKeyProvider();

        var ex = Should.Throw<InvalidOperationException>(() => modelBuilder.UseColumnEncryption(keyProvider));

        ex.Message.ShouldBe(
            "Property 'EncryptedIntEntity.Pin' is marked with [Encrypted] but is of type 'Int32', which is not supported. Only string properties can be encrypted.");
        keyProvider.RequestedTypes.ShouldBeEmpty();
    }

    [Fact]
    public void UseColumnEncryption_WithEncryptedStringInComplexType_ShouldThrowComplexTypeRefusalMessage()
    {
        var modelBuilder = SqliteConventionSetBuilder.CreateModelBuilder();
        modelBuilder.Entity<CustomerWithComplexCard>().ComplexProperty(c => c.Card);

        var ex = Should.Throw<InvalidOperationException>(() =>
            modelBuilder.UseColumnEncryption(new TestKeyProvider()));

        ex.Message.ShouldBe(
            "Property 'CardInfo.Number' is marked with [Encrypted] but belongs to a complex type, which is not supported. Move it to an entity or owned type as a string property, or remove the attribute.");
    }

    #endregion
}
