namespace Svc.BlobStorage.Tests;

public class BlobDataTests
{
    [Fact]
    public void BlobRequest_WithFileName_ShouldDetectFileType()
    {
        // Arrange & Act
        var request = new BlobRequest("test.txt");
        
        // Assert
        request.Name.ShouldBe("test.txt");
        request.Type.ShouldBe(BlobTypes.File);
    }

    [Fact]
    public void BlobRequest_WithDirectoryName_ShouldDetectDirectoryType()
    {
        // Arrange & Act
        var request = new BlobRequest("documents");
        
        // Assert
        request.Name.ShouldBe("documents");
        request.Type.ShouldBe(BlobTypes.Directory);
    }

    [Fact]
    public void BlobRequest_WithEmptyName_ShouldDetectDirectoryType()
    {
        // Arrange & Act
        var request = new BlobRequest("");
        
        // Assert
        request.Name.ShouldBe("");
        request.Type.ShouldBe(BlobTypes.Directory);
    }

    [Fact]
    public void BlobRequest_WithExplicitType_ShouldUseProvidedType()
    {
        // Arrange & Act
        var request = new BlobRequest("test.txt") { Type = BlobTypes.Directory };
        
        // Assert
        request.Name.ShouldBe("test.txt");
        request.Type.ShouldBe(BlobTypes.Directory);
    }

    [Fact]
    public void BlobDetails_ShouldInitializeCorrectly()
    {
        // Arrange
        var contentType = "text/plain";
        var contentLength = 1024L;
        var lastModified = DateTime.UtcNow;
        var createdOn = DateTime.UtcNow.AddDays(-1);
        
        // Act
        var details = new BlobDetails
        {
            ContentType = contentType,
            ContentLength = contentLength,
            LastModified = lastModified,
            CreatedOn = createdOn
        };
        
        // Assert
        details.ContentType.ShouldBe(contentType);
        details.ContentLength.ShouldBe(contentLength);
        details.LastModified.ShouldBe(lastModified);
        details.CreatedOn.ShouldBe(createdOn);
    }

    [Fact]
    public void BlobResult_ShouldInheritFromBlobRequest()
    {
        // Arrange & Act
        var result = new BlobResult("test.txt");
        
        // Assert
        result.Name.ShouldBe("test.txt");
        result.Type.ShouldBe(BlobTypes.File);
        result.Details.ShouldBeNull();
    }

    [Fact]
    public void BlobResult_WithDetails_ShouldSetDetailsCorrectly()
    {
        // Arrange
        var details = new BlobDetails
        {
            ContentType = "text/plain",
            ContentLength = 1024L,
            LastModified = DateTime.UtcNow,
            CreatedOn = DateTime.UtcNow.AddDays(-1)
        };
        
        // Act
        var result = new BlobResult("test.txt") { Details = details };
        
        // Assert
        result.Name.ShouldBe("test.txt");
        result.Details.ShouldNotBeNull();
        result.Details.ShouldBe(details);
    }

    [Fact]
    public void BlobDataResult_ShouldInheritFromBlobResult()
    {
        // Arrange
        var data = BinaryData.FromString("test content");
        
        // Act
        var result = new BlobDataResult("test.txt", data);
        
        // Assert
        result.Name.ShouldBe("test.txt");
        result.Data.ShouldBe(data);
        result.Type.ShouldBe(BlobTypes.File);
    }

    [Fact]
    public void BlobData_ShouldInheritFromBlobRequest()
    {
        // Arrange
        var data = BinaryData.FromString("test content");
        
        // Act
        var blobData = new BlobData("test.txt", data);
        
        // Assert
        blobData.Name.ShouldBe("test.txt");
        blobData.Data.ShouldBe(data);
        blobData.Type.ShouldBe(BlobTypes.File);
    }

    [Fact]
    public void BlobData_ShouldAutoDetectContentType()
    {
        // Arrange
        var data = BinaryData.FromString("test content");
        
        // Act
        var blobData = new BlobData("test.txt", data);
        
        // Assert
        blobData.ContentType.ShouldBe("text/plain");
    }

    [Fact]
    public void BlobData_WithCustomContentType_ShouldUseProvidedContentType()
    {
        // Arrange
        var data = BinaryData.FromString("test content");
        var customContentType = "application/custom";
        
        // Act
        var blobData = new BlobData("test.txt", data) { ContentType = customContentType };
        
        // Assert
        blobData.ContentType.ShouldBe(customContentType);
    }

    [Fact]
    public void BlobData_DefaultOverwrite_ShouldBeFalse()
    {
        // Arrange
        var data = BinaryData.FromString("test content");
        
        // Act
        var blobData = new BlobData("test.txt", data);
        
        // Assert
        blobData.Overwrite.ShouldBeFalse();
    }

    [Fact]
    public void BlobData_CanSetOverwrite()
    {
        // Arrange
        var data = BinaryData.FromString("test content");
        
        // Act
        var blobData = new BlobData("test.txt", data) { Overwrite = true };
        
        // Assert
        blobData.Overwrite.ShouldBeTrue();
    }

    [Theory]
    [InlineData("document.pdf", "application/pdf")]
    [InlineData("image.png", "image/png")]
    [InlineData("data.json", "application/json")]
    public void BlobData_ShouldDetectContentTypeFromExtension(string fileName, string expectedContentType)
    {
        // Arrange
        var data = BinaryData.FromString("test content");
        
        // Act
        var blobData = new BlobData(fileName, data);
        
        // Assert
        blobData.ContentType.ShouldBe(expectedContentType);
    }
}