using DKNet.EfCore.DtoGenerator;

namespace EfCore.DtoGenerator.Tests;

public class GenerateEventAttributeTests
{
    #region Methods

    [Fact]
    public void DefaultState_UsesNullSuffixNoKindsAndEmptyCollections()
    {
        // Arrange & Act
        var attribute = new GenerateEventAttribute();

        // Assert
        attribute.NameSuffix.ShouldBeNull();
        attribute.Kinds.ShouldBe(default(EventKinds));
        attribute.Properties.ShouldBeEmpty();
        attribute.Include.ShouldBeEmpty();
        attribute.Exclude.ShouldBeEmpty();
        attribute.IgnoreComplexType.ShouldBeFalse();
    }

    [Fact]
    public void ObjectInitializer_StoresDeclaredConfiguration()
    {
        // Arrange & Act
        var attribute = new GenerateEventAttribute
        {
            NameSuffix = "StatusChanged",
            Kinds = EventKinds.Updated,
            Properties = new[] { "Status" },
            Include = new[] { "Status" },
            Exclude = new[] { "Id" },
            IgnoreComplexType = true,
        };

        // Assert
        attribute.NameSuffix.ShouldBe("StatusChanged");
        attribute.Kinds.ShouldBe(EventKinds.Updated);
        attribute.Properties.ShouldBe(new[] { "Status" });
        attribute.Include.ShouldBe(new[] { "Status" });
        attribute.Exclude.ShouldBe(new[] { "Id" });
        attribute.IgnoreComplexType.ShouldBeTrue();
    }

    [Fact]
    public void Kinds_CreatedUpdatedValuesMatchLifecycleOperations()
    {
        // Arrange & Act
        var created = EventKinds.Created;
        var updated = EventKinds.Updated;
        var deleted = EventKinds.Deleted;

        // Assert
        created.HasFlag(updated).ShouldBeFalse();
        updated.HasFlag(deleted).ShouldBeFalse();
        (created | updated | deleted).HasFlag(created).ShouldBeTrue();
        (created | updated | deleted).HasFlag(updated).ShouldBeTrue();
        (created | updated | deleted).HasFlag(deleted).ShouldBeTrue();
    }

    [Fact]
    public void IsClassLevelAndRepeatable()
    {
        // Arrange & Act
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(GenerateEventAttribute), typeof(AttributeUsageAttribute));

        // Assert
        usage.ShouldNotBeNull();
        usage.ValidOn.ShouldBe(AttributeTargets.Class);
        usage.AllowMultiple.ShouldBeTrue();
        usage.Inherited.ShouldBeFalse();
    }

    #endregion
}