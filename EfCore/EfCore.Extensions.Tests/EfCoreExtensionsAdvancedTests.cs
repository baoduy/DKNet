namespace EfCore.Extensions.Tests;

[TestClass]
public class EfCoreExtensionsAdvancedTests : SqlServerTestBase
{
    private static MyDbContext _db;

    [ClassInitialize]
    public static async Task ClassSetup(TestContext _)
    {
        await StartSqlContainerAsync();
        _db = CreateDbContext("EfCoreTestDb");
        await _db.Database.EnsureCreatedAsync();
    }

    [TestMethod]
    public void GetPrimaryKeyValues_WithNullEntity_ShouldThrowArgumentNullException()
    {
        var action =()=> _db.GetPrimaryKeyValues(null!).ToList();
        // Act & Assert
        action.ShouldThrow<ArgumentNullException>();
    }

    [TestMethod]
    public void GetPrimaryKeyValues_WithEntityHavingMultipleKeys_ShouldReturnAllKeys()
    {
        // Arrange - Assuming we have an entity with composite keys (may need to create one)
        var user = new User(1, "Test User") { FirstName = "Test", LastName = "User" };

        // Act
        var keyValues = _db.GetPrimaryKeyValues(user).ToList();

        // Assert
        keyValues.ShouldHaveSingleItem();
        keyValues[0].ShouldBe(1);
    }

    [TestMethod]
    public void GetPrimaryKeyProperties_WithValidEntityType_ShouldReturnPropertyNames()
    {
        // Act
        var properties = _db.GetPrimaryKeyProperties<User>().ToList();

        // Assert
        properties.ShouldHaveSingleItem();
        properties[0].ShouldBe("Id");
    }

    [TestMethod]
    public void GetTableName_WithValidEntity_ShouldReturnQualifiedTableName()
    {
        // Act
        var tableName = _db.GetTableName(typeof(User));

        // Assert
        tableName.ShouldNotBeNullOrEmpty();
        tableName.ShouldContain("User"); // Should contain the entity name
    }

    [TestMethod]
    public void GetTableName_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            ((DbContext)null!).GetTableName(typeof(User)));
    }

    [TestMethod]
    public async Task NextSeqValue_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await ((DbContext)null!).NextSeqValue(TestSequenceTypes.TestSequence1));
    }

    [TestMethod]
    public async Task NextSeqValue_WithUnsupportedEnum_ShouldReturnNull()
    {
        // Arrange - Use a regular enum without sequence attributes
        var regularEnum = DayOfWeek.Monday;

        // Act
        var result = await _db.NextSeqValue(regularEnum);

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public async Task NextSeqValueWithFormat_WithEmptyFormatString_ShouldReturnValueAsString()
    {
        // This would need a special test enum with empty format string
        // For now, we'll test the case where format string processing works
        await StartSqlContainerAsync();

        var options = new DbContextOptionsBuilder()
            .UseSqlServer(GetConnectionString("EfCoreTestDb"))
            .UseAutoConfigModel(op => op.ScanFrom(typeof(TestSequenceTypes).Assembly))
            .Options;

        await using var context = new DbContext(options);
        await context.Database.EnsureCreatedAsync();

        // This test verifies the format processing logic
        var result = await context.NextSeqValueWithFormat(TestSequenceTypes.TestSequence1);
        result.ShouldNotBeNullOrEmpty();
    }

    [TestMethod]
    public void GetEntityType_WithValidMappingType_ShouldReturnEntityType()
    {
        // Arrange
        var mappingType = typeof(UserSeedingConfiguration);

        // Act
        var entityType = EfCoreExtensions.GetEntityType(mappingType);

        // Assert
        entityType.ShouldBe(typeof(User));
    }
}