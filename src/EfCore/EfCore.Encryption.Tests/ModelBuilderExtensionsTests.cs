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
    [Key]
    public int Id { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    [Encrypted]
    public string? SocialSecurityNumber { get; set; }
    
    [Encrypted]
    public string? CreditCardNumber { get; set; }
}

public class ProductEntity
{
    [Key]
    public int Id { get; set; }
    
    [Encrypted]
    public string? SecretFormula { get; set; }
    
    public decimal Price { get; set; }
    
    public string Description { get; set; } = string.Empty;
}

public class NoEncryptionEntity
{
    [Key]
    public int Id { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
}

// Test DbContext
public class TestDbContext : DbContext
{
    private readonly IEncryptionKeyProvider? _keyProvider;

    public TestDbContext(DbContextOptions<TestDbContext> options, IEncryptionKeyProvider? keyProvider = null) : base(options)
    {
        _keyProvider = keyProvider;
    }

    public DbSet<PersonEntity> People { get; set; }
    public DbSet<ProductEntity> Products { get; set; }
    public DbSet<NoEncryptionEntity> Items { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        if (_keyProvider != null)
        {
            modelBuilder.UseColumnEncryption(_keyProvider);
        }
    }
}

// Test key provider
internal class TestKeyProvider : EncryptionKeyProvider
{
    private readonly byte[] _defaultKey;
    private readonly Dictionary<Type, byte[]> _typeKeys = new();

    public TestKeyProvider()
    {
        _defaultKey = new byte[32];
        RandomNumberGenerator.Fill(_defaultKey);
    }

    public void SetKeyForType(Type type, byte[] key)
    {
        _typeKeys[type] = key;
    }

    public override byte[] GetKey(Type entityType)
    {
        return _typeKeys.TryGetValue(entityType, out var key) ? key : _defaultKey;
    }
}

public class ModelBuilderExtensionsTests
{
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
    public void UseColumnEncryption_WithNullKeyProvider_ShouldThrowArgumentNullException()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => modelBuilder.UseColumnEncryption(null!))
            .ParamName.ShouldBe("encryptionKeyProvider");
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

    [Fact]
    public void UseColumnEncryption_ShouldApplyConverterToEncryptedProperties()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
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
    public void UseColumnEncryption_ShouldNotApplyConverterToNonEncryptedProperties()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
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
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
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
    public void UseColumnEncryption_WithMultipleEntities_ShouldApplyToAll()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
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
    public void EncryptedProperty_IntegrationTest_ShouldEncryptAndDecrypt()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
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
    public void UseColumnEncryption_ShouldIgnoreKeyProperties()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
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
    public void UseColumnEncryption_WithEntityWithoutEncryptedProperties_ShouldNotFail()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
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
    public void EncryptedProperty_WithNullValue_ShouldHandleCorrectly()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
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
    public void EncryptedProperty_WithEmptyString_ShouldHandleCorrectly()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
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
}
