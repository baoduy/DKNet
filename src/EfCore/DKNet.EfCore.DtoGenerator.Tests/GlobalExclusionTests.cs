using DKNet.EfCore.DtoGenerator.Tests.Features;
using Shouldly;

namespace DKNet.EfCore.DtoGenerator.Tests;

/// <summary>
///     Tests for global exclusion functionality in the DtoGenerator.
///     Global exclusions are configured in the .csproj file via the DtoGenerator_GlobalExclusions property.
/// </summary>
public class GlobalExclusionTests
{
    #region Methods

    [Fact]
    public void GlobalAndLocalExclusionTestDto_ShouldCombineGlobalAndLocalExclusions()
    {
        // Assert - Verify properties exist or don't exist at compile time
        var hasId = typeof(GlobalAndLocalExclusionTestDto).GetProperty("Id") != null;
        var hasName = typeof(GlobalAndLocalExclusionTestDto).GetProperty("Name") != null;
        var hasDescription = typeof(GlobalAndLocalExclusionTestDto).GetProperty("Description") != null;

        // These should be excluded (global + local)
        var hasCreatedBy = typeof(GlobalAndLocalExclusionTestDto).GetProperty("CreatedBy") != null;
        var hasUpdatedBy = typeof(GlobalAndLocalExclusionTestDto).GetProperty("UpdatedBy") != null;
        var hasCreatedAt = typeof(GlobalAndLocalExclusionTestDto).GetProperty("CreatedAt") != null;
        var hasUpdatedAt = typeof(GlobalAndLocalExclusionTestDto).GetProperty("UpdatedAt") != null;
        var hasIsActive = typeof(GlobalAndLocalExclusionTestDto).GetProperty("IsActive") != null;

        // Assert - Properties that should exist
        hasId.ShouldBeTrue("Id should be included");
        hasName.ShouldBeTrue("Name should be included");
        hasDescription.ShouldBeTrue("Description should be included");

        // Assert - Properties that should be excluded (global)
        hasCreatedBy.ShouldBeFalse("CreatedBy should be globally excluded");
        hasUpdatedBy.ShouldBeFalse("UpdatedBy should be globally excluded");
        hasCreatedAt.ShouldBeFalse("CreatedAt should be globally excluded");
        hasUpdatedAt.ShouldBeFalse("UpdatedAt should be globally excluded");

        // Assert - Property that should be locally excluded
        hasIsActive.ShouldBeFalse("IsActive should be locally excluded");
    }

    [Fact]
    public void GlobalExclusionConfiguration_ClassShouldExist()
    {
        // Assert - Verify the GlobalDtoConfiguration class exists for documentation purposes
        var configurationType = typeof(GlobalDtoConfiguration);
        configurationType.ShouldNotBeNull();
        configurationType.Name.ShouldBe("GlobalDtoConfiguration");
    }

    [Fact]
    public void GlobalExclusionTestDto_ShouldExcludeGlobalProperties()
    {
        // Assert - Verify properties exist or don't exist at compile time
        var hasId = typeof(GlobalExclusionTestDto).GetProperty("Id") != null;
        var hasName = typeof(GlobalExclusionTestDto).GetProperty("Name") != null;
        var hasDescription = typeof(GlobalExclusionTestDto).GetProperty("Description") != null;
        var hasIsActive = typeof(GlobalExclusionTestDto).GetProperty("IsActive") != null;

        // These should be excluded by global configuration
        var hasCreatedBy = typeof(GlobalExclusionTestDto).GetProperty("CreatedBy") != null;
        var hasUpdatedBy = typeof(GlobalExclusionTestDto).GetProperty("UpdatedBy") != null;
        var hasCreatedAt = typeof(GlobalExclusionTestDto).GetProperty("CreatedAt") != null;
        var hasUpdatedAt = typeof(GlobalExclusionTestDto).GetProperty("UpdatedAt") != null;

        // Assert - Properties that should exist
        hasId.ShouldBeTrue("Id should be included");
        hasName.ShouldBeTrue("Name should be included");
        hasDescription.ShouldBeTrue("Description should be included");
        hasIsActive.ShouldBeTrue("IsActive should be included");

        // Assert - Properties that should be globally excluded
        hasCreatedBy.ShouldBeFalse("CreatedBy should be globally excluded");
        hasUpdatedBy.ShouldBeFalse("UpdatedBy should be globally excluded");
        hasCreatedAt.ShouldBeFalse("CreatedAt should be globally excluded");
        hasUpdatedAt.ShouldBeFalse("UpdatedAt should be globally excluded");
    }

    [Fact]
    public void IncludeOverridesGlobalExclusionTestDto_ShouldOnlyIncludeSpecifiedProperties()
    {
        // Assert - Verify properties exist or don't exist at compile time
        var hasId = typeof(IncludeOverridesGlobalExclusionTestDto).GetProperty("Id") != null;
        var hasName = typeof(IncludeOverridesGlobalExclusionTestDto).GetProperty("Name") != null;
        var hasCreatedAt = typeof(IncludeOverridesGlobalExclusionTestDto).GetProperty("CreatedAt") != null;

        // These should not be included
        var hasDescription = typeof(IncludeOverridesGlobalExclusionTestDto).GetProperty("Description") != null;
        var hasIsActive = typeof(IncludeOverridesGlobalExclusionTestDto).GetProperty("IsActive") != null;
        var hasCreatedBy = typeof(IncludeOverridesGlobalExclusionTestDto).GetProperty("CreatedBy") != null;
        var hasUpdatedBy = typeof(IncludeOverridesGlobalExclusionTestDto).GetProperty("UpdatedBy") != null;
        var hasUpdatedAt = typeof(IncludeOverridesGlobalExclusionTestDto).GetProperty("UpdatedAt") != null;

        // Assert - Only included properties should exist
        hasId.ShouldBeTrue("Id should be included");
        hasName.ShouldBeTrue("Name should be included");
        hasCreatedAt.ShouldBeTrue("CreatedAt should be included (Include overrides global exclusion)");

        // Assert - All other properties should not exist
        hasDescription.ShouldBeFalse("Description should not be included");
        hasIsActive.ShouldBeFalse("IsActive should not be included");
        hasCreatedBy.ShouldBeFalse("CreatedBy should not be included");
        hasUpdatedBy.ShouldBeFalse("UpdatedBy should not be included");
        hasUpdatedAt.ShouldBeFalse("UpdatedAt should not be included");
    }

    #endregion
}