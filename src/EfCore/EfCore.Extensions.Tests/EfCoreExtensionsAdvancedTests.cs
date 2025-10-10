using EfCore.Extensions.Tests.TestEntities.Mappers;

namespace EfCore.Extensions.Tests;

public class EfCoreExtensionsAdvancedTests(MemoryFixture fixture) : IClassFixture<MemoryFixture>
{
    private readonly MyDbContext _db = fixture.Db!;


    [Fact]
    public void GetPrimaryKeyValues_WithNullEntity_ShouldThrowArgumentNullException()
    {
        var action = () => _db.GetPrimaryKeyValues(null!).ToList();
        // Act & Assert
        action.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void GetPrimaryKeyValues_WithEntityHavingMultipleKeys_ShouldReturnAllKeys()
    {
        // Arrange - Assuming we have an entity with composite keys (may need to create one)
        var user = new User(1, "Test User") { FirstName = "Test", LastName = "User" };

        // Act
        var keyValues = _db.GetPrimaryKeyValues(user).ToList();

        // Assert
        keyValues.ShouldHaveSingleItem();
        keyValues[0].Value.ShouldBe(1);
    }

    [Fact]
    public void GetPrimaryKeyProperties_WithValidEntityType_ShouldReturnPropertyNames()
    {
        // Act
        var properties = _db.GetPrimaryKeyProperties<User>().ToList();

        // Assert
        properties.ShouldHaveSingleItem();
        properties[0].ShouldBe("Id");
    }

    [Fact]
    public void GetTableName_WithValidEntity_ShouldReturnQualifiedTableName()
    {
        // Act
        var tableName = _db.GetTableName(typeof(User));

        // Assert
        tableName.ShouldNotBeNullOrEmpty();
        tableName.ShouldContain("User"); // Should contain the entity name
    }

    [Fact]
    public void GetTableName_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            ((DbContext)null!).GetTableName(typeof(User)));
    }

    [Fact]
    public async Task NextSeqValue_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await ((DbContext)null!).NextSeqValue(TestSequenceTypes.TestSequence1));
    }

    [Fact]
    public async Task NextSeqValue_WithUnsupportedEnum_ShouldReturnNull()
    {
        // Arrange - Use a regular enum without sequence attributes
        var regularEnum = DayOfWeek.Monday;

        // Act
        var result = await _db.NextSeqValue(regularEnum);

        // Assert
        result.ShouldBeNull();
    }


    [Fact]
    public void GetEntityType_WithValidMappingType_ShouldReturnEntityType()
    {
        // Arrange
        var mappingType = typeof(AddressEntityMapper);

        // Act
        var entityType = EfCoreExtensions.GetEntityType(mappingType);

        // Assert
        entityType.ShouldBe(typeof(Address));
    }
}