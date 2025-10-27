namespace Svc.BlobStorage.Tests;

public class BlobServiceOptionsTests
{
    #region Methods

    [Fact]
    public void BlobServiceOptions_CanSetAllProperties()
    {
        // Arrange
        var extensions = new[] { ".txt", ".pdf", ".jpg" };
        var maxLength = 255;
        var maxSize = 10;

        // Act
        var options = new BlobServiceOptions
        {
            IncludedExtensions = extensions,
            MaxFileNameLength = maxLength,
            MaxFileSizeInMb = maxSize
        };

        // Assert
        options.IncludedExtensions.ShouldBe(extensions);
        options.MaxFileNameLength.ShouldBe(maxLength);
        options.MaxFileSizeInMb.ShouldBe(maxSize);
    }

    [Fact]
    public void BlobServiceOptions_CanSetIncludedExtensions()
    {
        // Arrange
        var extensions = new[] { ".txt", ".pdf", ".jpg" };

        // Act
        var options = new BlobServiceOptions
        {
            IncludedExtensions = extensions
        };

        // Assert
        options.IncludedExtensions.ShouldBe(extensions);
    }

    [Fact]
    public void BlobServiceOptions_CanSetMaxFileNameLength()
    {
        // Arrange
        var maxLength = 255;

        // Act
        var options = new BlobServiceOptions
        {
            MaxFileNameLength = maxLength
        };

        // Assert
        options.MaxFileNameLength.ShouldBe(maxLength);
    }

    [Fact]
    public void BlobServiceOptions_CanSetMaxFileSizeInMb()
    {
        // Arrange
        var maxSize = 10;

        // Act
        var options = new BlobServiceOptions
        {
            MaxFileSizeInMb = maxSize
        };

        // Assert
        options.MaxFileSizeInMb.ShouldBe(maxSize);
    }

    [Fact]
    public void BlobServiceOptions_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var options = new BlobServiceOptions();

        // Assert
        options.IncludedExtensions.ShouldNotBeNull();
        options.IncludedExtensions.ShouldBeEmpty();
        options.MaxFileNameLength.ShouldBe(0);
        options.MaxFileSizeInMb.ShouldBe(0);
    }

    #endregion
}