using FluentValidation.TestHelper;
using SlimBus.AppServices.Profiles.V1.Queries;

namespace SlimBus.App.Tests.Unit;

public class ValidatorTests
{
    [Fact]
    public void ProfilePageableQueryValidator_PageSize_ShouldHaveError_WhenNull()
    {
        // Arrange
        var validator = new ProfilePageableQueryValidator();
        var query = new PageProfilePageQuery { PageSize = 0 };

        // Act & Assert
        var result = validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void ProfilePageableQueryValidator_PageSize_ShouldHaveError_WhenLessThan1()
    {
        // Arrange
        var validator = new ProfilePageableQueryValidator();
        var query = new PageProfilePageQuery { PageSize = 0 };

        // Act & Assert
        var result = validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void ProfilePageableQueryValidator_PageSize_ShouldHaveError_WhenGreaterThan1000()
    {
        // Arrange
        var validator = new ProfilePageableQueryValidator();
        var query = new PageProfilePageQuery { PageSize = 1001 };

        // Act & Assert
        var result = validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public void ProfilePageableQueryValidator_PageSize_ShouldNotHaveError_WhenInValidRange(int pageSize)
    {
        // Arrange
        var validator = new ProfilePageableQueryValidator();
        var query = new PageProfilePageQuery { PageSize = pageSize, PageIndex = 0 };

        // Act & Assert
        var result = validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void ProfilePageableQueryValidator_PageIndex_ShouldHaveError_WhenLessThan0()
    {
        // Arrange
        var validator = new ProfilePageableQueryValidator();
        var query = new PageProfilePageQuery { PageIndex = -1, PageSize = 100 };

        // Act & Assert
        var result = validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageIndex);
    }

    [Fact]
    public void ProfilePageableQueryValidator_PageIndex_ShouldHaveError_WhenGreaterThan1000()
    {
        // Arrange
        var validator = new ProfilePageableQueryValidator();
        var query = new PageProfilePageQuery { PageIndex = 1001, PageSize = 100 };

        // Act & Assert
        var result = validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageIndex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(500)]
    [InlineData(1000)]
    public void ProfilePageableQueryValidator_PageIndex_ShouldNotHaveError_WhenInValidRange(int pageIndex)
    {
        // Arrange
        var validator = new ProfilePageableQueryValidator();
        var query = new PageProfilePageQuery { PageIndex = pageIndex, PageSize = 100 };

        // Act & Assert
        var result = validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.PageIndex);
    }

    [Fact]
    public void ProfilePageableQueryValidator_ShouldNotHaveAnyErrors_WhenBothValuesAreValid()
    {
        // Arrange
        var validator = new ProfilePageableQueryValidator();
        var query = new PageProfilePageQuery { PageIndex = 5, PageSize = 50 };

        // Act & Assert
        var result = validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }
}