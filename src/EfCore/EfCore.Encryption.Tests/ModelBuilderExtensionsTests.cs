using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using DKNet.EfCore.Encryption.Attributes;
using DKNet.EfCore.Encryption.Encryption;
using DKNet.EfCore.Encryption.Extensions;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace EfCore.Encryption.Tests;

// Test entities
public class PersonEntity
{
    #region Properties

    [Encrypted] public string? CreditCardNumber { get; set; }

    [Key] public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    [Encrypted] public string? SocialSecurityNumber { get; set; }

    #endregion
}

public class ProductEntity
{
    #region Properties

    public string Description { get; set; } = string.Empty;

    [Key] public int Id { get; set; }

    public decimal Price { get; set; }

    [Encrypted] public string? SecretFormula { get; set; }

    #endregion
}

public class NoEncryptionEntity
{
    #region Properties

    public string Description { get; set; } = string.Empty;

    [Key] public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    #endregion
}

// Test DbContext
public class TestDbContext(DbContextOptions<TestDbContext> options, IEncryptionKeyProvider? keyProvider = null)
    : DbContext(options)
{
    #region Properties

    public DbSet<NoEncryptionEntity> Items { get; set; }

    public DbSet<PersonEntity> People { get; set; }

    public DbSet<ProductEntity> Products { get; set; }

    #endregion

    #region Methods

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (keyProvider != null) modelBuilder.UseColumnEncryption(keyProvider);
    }

    #endregion
}

// Test key provider
internal class TestKeyProvider : EncryptionKeyProvider
{
    #region Fields

    private readonly byte[] _defaultKey;
    private readonly Dictionary<Type, byte[]> _typeKeys = new();

    #endregion

    #region Constructors

    public TestKeyProvider()
    {
        _defaultKey = new byte[32];
        RandomNumberGenerator.Fill(_defaultKey);
    }

    #endregion

    #region Methods

    public override byte[] GetKey(Type entityType) =>
        _typeKeys.TryGetValue(entityType, out var key) ? key : _defaultKey;

    public void SetKeyForType(Type type, byte[] key)
    {
        _typeKeys[type] = key;
    }

    #endregion
}

public class ModelBuilderExtensionsTests
{
    #region Methods

    [Fact]
    public void EncryptedProperty_IntegrationTest_ShouldEncryptAndDecrypt()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var keyProvider = new TestKeyProvider();

        // Act & Assert - Save
        using (var context = new TestDbContext(options, keyProvider))
        {
            var person = new PersonEntity
            {
                Name = "John Doe",
                SocialSecurityNumber = "123-45-6789",
                CreditCardNumber = "4111-1111-1111-1111"
            };

            context.People.Add(person);
            context.SaveChanges();
        }

        // Act & Assert - Retrieve
        using (var context = new TestDbContext(options, keyProvider))
        {
            var person = context.People.FirstOrDefault();

            person.ShouldNotBeNull();
            person.Name.ShouldBe("John Doe");
            person.SocialSecurityNumber.ShouldBe("123-45-6789");
            person.CreditCardNumber.ShouldBe("4111-1111-1111-1111");
        }
    }

    [Fact]
    public void EncryptedProperty_WithEmptyString_ShouldHandleCorrectly()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var keyProvider = new TestKeyProvider();

        // Act & Assert - Save with empty string
        using (var context = new TestDbContext(options, keyProvider))
        {
            var person = new PersonEntity
            {
                Name = "Test User",
                SocialSecurityNumber = string.Empty,
                CreditCardNumber = string.Empty
            };

            context.People.Add(person);
            context.SaveChanges();
        }

        // Act & Assert - Retrieve
        using (var context = new TestDbContext(options, keyProvider))
        {
            var person = context.People.FirstOrDefault();

            person.ShouldNotBeNull();
            person.SocialSecurityNumber.ShouldBe(string.Empty);
            person.CreditCardNumber.ShouldBe(string.Empty);
        }
    }

    [Fact]
    public void EncryptedProperty_WithNullValue_ShouldHandleCorrectly()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var keyProvider = new TestKeyProvider();

        // Act & Assert - Save with null
        using (var context = new TestDbContext(options, keyProvider))
        {
            var person = new PersonEntity
            {
                Name = "Jane Doe",
                SocialSecurityNumber = null,
                CreditCardNumber = null
            };

            context.People.Add(person);
            context.SaveChanges();
        }

        // Act & Assert - Retrieve
        using (var context = new TestDbContext(options, keyProvider))
        {
            var person = context.People.FirstOrDefault();

            person.ShouldNotBeNull();
            person.Name.ShouldBe("Jane Doe");
            person.SocialSecurityNumber.ShouldBeNull();
            person.CreditCardNumber.ShouldBeNull();
        }
    }

    [Fact]
    public void UseColumnEncryption_ShouldApplyConverterToEncryptedProperties()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var keyProvider = new TestKeyProvider();

        using var context = new TestDbContext(options, keyProvider);

        // Trigger model creation
        _ = context.Model;

        var personEntityType = context.Model.FindEntityType(typeof(PersonEntity));

        // Assert
        personEntityType.ShouldNotBeNull();

        var ssnProperty = personEntityType.FindProperty(nameof(PersonEntity.SocialSecurityNumber));
        ssnProperty.ShouldNotBeNull();
        ssnProperty.GetValueConverter().ShouldNotBeNull();

        var ccProperty = personEntityType.FindProperty(nameof(PersonEntity.CreditCardNumber));
        ccProperty.ShouldNotBeNull();
        ccProperty.GetValueConverter().ShouldNotBeNull();
    }

    [Fact]
    public void UseColumnEncryption_ShouldIgnoreKeyProperties()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var keyProvider = new TestKeyProvider();

        using var context = new TestDbContext(options, keyProvider);

        // Trigger model creation
        _ = context.Model;

        var personEntityType = context.Model.FindEntityType(typeof(PersonEntity));

        // Assert
        personEntityType.ShouldNotBeNull();

        var idProperty = personEntityType.FindProperty(nameof(PersonEntity.Id));
        idProperty.ShouldNotBeNull();
        idProperty.GetValueConverter().ShouldBeNull();
    }

    [Fact]
    public void UseColumnEncryption_ShouldNotApplyConverterToNonEncryptedProperties()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var keyProvider = new TestKeyProvider();

        using var context = new TestDbContext(options, keyProvider);

        // Trigger model creation
        _ = context.Model;

        var personEntityType = context.Model.FindEntityType(typeof(PersonEntity));

        // Assert
        personEntityType.ShouldNotBeNull();

        var nameProperty = personEntityType.FindProperty(nameof(PersonEntity.Name));
        nameProperty.ShouldNotBeNull();
        nameProperty.GetValueConverter().ShouldBeNull();
    }

    [Fact]
    public void UseColumnEncryption_ShouldNotApplyConverterToNonStringProperties()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var keyProvider = new TestKeyProvider();

        using var context = new TestDbContext(options, keyProvider);

        // Trigger model creation
        _ = context.Model;

        var productEntityType = context.Model.FindEntityType(typeof(ProductEntity));

        // Assert
        productEntityType.ShouldNotBeNull();

        var priceProperty = productEntityType.FindProperty(nameof(ProductEntity.Price));
        priceProperty.ShouldNotBeNull();
        priceProperty.GetValueConverter().ShouldBeNull();
    }

    [Fact]
    public void UseColumnEncryption_WithEntityWithoutEncryptedProperties_ShouldNotFail()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var keyProvider = new TestKeyProvider();

        // Act & Assert
        Should.NotThrow(() =>
        {
            using var context = new TestDbContext(options, keyProvider);
            _ = context.Model;
        });
    }

    [Fact]
    public void UseColumnEncryption_WithMultipleEntities_ShouldApplyToAll()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var keyProvider = new TestKeyProvider();

        using var context = new TestDbContext(options, keyProvider);

        // Trigger model creation
        _ = context.Model;

        // Assert
        var personEntityType = context.Model.FindEntityType(typeof(PersonEntity));
        personEntityType.ShouldNotBeNull();
        personEntityType.FindProperty(nameof(PersonEntity.SocialSecurityNumber))
            ?.GetValueConverter().ShouldNotBeNull();

        var productEntityType = context.Model.FindEntityType(typeof(ProductEntity));
        productEntityType.ShouldNotBeNull();
        productEntityType.FindProperty(nameof(ProductEntity.SecretFormula))
            ?.GetValueConverter().ShouldNotBeNull();
    }

    [Fact]
    public void UseColumnEncryption_WithNullKeyProvider_ShouldThrowArgumentNullException()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => modelBuilder.UseColumnEncryption(null!))
            .ParamName.ShouldBe("encryptionKeyProvider");
    }

    [Fact]
    public void UseColumnEncryption_WithNullModelBuilder_ShouldThrowArgumentNullException()
    {
        // Arrange
        ModelBuilder? modelBuilder = null;
        var keyProvider = new TestKeyProvider();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => modelBuilder!.UseColumnEncryption(keyProvider))
            .ParamName.ShouldBe("modelBuilder");
    }

    [Fact]
    public void UseColumnEncryption_WithValidParameters_ShouldNotThrow()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<PersonEntity>();
        var keyProvider = new TestKeyProvider();

        // Act & Assert
        Should.NotThrow(() => modelBuilder.UseColumnEncryption(keyProvider));
    }

    #endregion
}