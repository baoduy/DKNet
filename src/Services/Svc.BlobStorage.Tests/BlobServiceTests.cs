using System.Runtime.CompilerServices;

namespace Svc.BlobStorage.Tests;

public class BlobServiceTests
{
    #region Methods

    [Theory]
    [InlineData("file.txt", "/file.txt")]
    [InlineData("/file.txt", "/file.txt")]
    [InlineData("folder/file.txt", "/folder/file.txt")]
    [InlineData("/folder/file.txt", "/folder/file.txt")]
    [InlineData("", "/")]
    public void GetBlobLocation_ShouldAddLeadingSlashIfMissing(string itemName, string expectedLocation)
    {
        // Arrange
        var options = new BlobServiceOptions();
        var service = new TestBlobService(options);
        var request = new BlobRequest(itemName);

        // Act
        var result = service.TestGetBlobLocation(request);

        // Assert
        result.ShouldBe(expectedLocation);
    }

    [Fact]
    public async Task GetItemAsync_WithNoResults_ShouldReturnNull()
    {
        // Arrange
        var options = new BlobServiceOptions();
        var service = new TestBlobService(options);
        var request = new BlobRequest("nonexistent.txt");

        // Act
        var result = await service.GetItemAsync(request);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ValidateFile_WithCaseInsensitiveExtension_ShouldNotThrow()
    {
        // Arrange
        var options = new BlobServiceOptions
        {
            IncludedExtensions = [".txt"]
        };
        var service = new TestBlobService(options);
        var data = BinaryData.FromString("test content");
        var blobData = new BlobData("test.TXT", data);

        // Act & Assert
        Should.NotThrow(() => service.TestValidateFile(blobData));
    }

    [Fact]
    public void ValidateFile_WithFileNameTooLong_ShouldThrowFileLoadException()
    {
        // Arrange
        var options = new BlobServiceOptions
        {
            IncludedExtensions = [".txt"],
            MaxFileNameLength = 5
        };
        var service = new TestBlobService(options);
        var data = BinaryData.FromString("test content");
        var blobData = new BlobData("verylongfilename.txt", data);

        // Act & Assert
        var exception = Should.Throw<FileLoadException>(() => service.TestValidateFile(blobData));
        exception.Message.ShouldBe("File name is invalid.");
    }

    [Fact]
    public void ValidateFile_WithFileTooLarge_ShouldThrowFileLoadException()
    {
        // Arrange
        var options = new BlobServiceOptions
        {
            IncludedExtensions = [".txt"],
            MaxFileSizeInMb = 1
        };
        var service = new TestBlobService(options);

        // Create a large file (2MB)
        var largeContent = new string('x', 2 * 1024 * 1024);
        var data = BinaryData.FromString(largeContent);
        var blobData = new BlobData("test.txt", data);

        // Act & Assert
        var exception = Should.Throw<FileLoadException>(() => service.TestValidateFile(blobData));
        exception.Message.ShouldBe("File size is invalid.");
    }

    [Fact]
    public void ValidateFile_WithInvalidExtension_ShouldThrowFileLoadException()
    {
        // Arrange
        var options = new BlobServiceOptions
        {
            IncludedExtensions = [".txt"]
        };
        var service = new TestBlobService(options);
        var data = BinaryData.FromString("test content");
        var blobData = new BlobData("test.pdf", data);

        // Act & Assert
        var exception = Should.Throw<FileLoadException>(() => service.TestValidateFile(blobData));
        exception.Message.ShouldBe("File extension is invalid.");
    }

    [Fact]
    public void ValidateFile_WithNoExtension_ShouldThrowFileLoadException()
    {
        // Arrange
        var options = new BlobServiceOptions
        {
            IncludedExtensions = [".txt"]
        };
        var service = new TestBlobService(options);
        var data = BinaryData.FromString("test content");
        var blobData = new BlobData("testfile", data);

        // Act & Assert
        var exception = Should.Throw<FileLoadException>(() => service.TestValidateFile(blobData));
        exception.Message.ShouldBe("File extension is invalid.");
    }

    [Fact]
    public void ValidateFile_WithNoMaximumLimits_ShouldOnlyCheckExtension()
    {
        // Arrange
        var options = new BlobServiceOptions
        {
            IncludedExtensions = [".txt"],
            MaxFileNameLength = 0, // No limit
            MaxFileSizeInMb = 0 // No limit
        };
        var service = new TestBlobService(options);

        // Create a large file with long name
        var largeContent = new string('x', 10 * 1024 * 1024); // 10MB
        var data = BinaryData.FromString(largeContent);
        var blobData = new BlobData("very_long_file_name_that_exceeds_typical_limits.txt", data);

        // Act & Assert
        Should.NotThrow(() => service.TestValidateFile(blobData));
    }

    [Fact]
    public void ValidateFile_WithValidFile_ShouldNotThrow()
    {
        // Arrange
        var options = new BlobServiceOptions
        {
            IncludedExtensions = [".txt"],
            MaxFileNameLength = 100,
            MaxFileSizeInMb = 10
        };
        var service = new TestBlobService(options);
        var data = BinaryData.FromString("test content");
        var blobData = new BlobData("test.txt", data);

        // Act & Assert
        Should.NotThrow(() => service.TestValidateFile(blobData));
    }

    #endregion

    private class TestBlobService : BlobService
    {
        #region Constructors

        public TestBlobService(BlobServiceOptions options) : base(options)
        {
        }

        #endregion

        #region Methods

        public override Task<bool> CheckExistsAsync(BlobRequest blob, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public override Task<bool> DeleteAsync(BlobRequest blob, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public override Task<BlobDataResult?>
            GetAsync(BlobRequest blob, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public override Task<Uri> GetPublicAccessUrl(
            BlobRequest blob,
            TimeSpan? expiresFromNow = null,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public override async IAsyncEnumerable<BlobResult> ListItemsAsync(
            BlobRequest blob,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Return empty enumerable for testing
            await Task.CompletedTask;
            yield break;
        }

        public override Task<string> SaveAsync(BlobData blob, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        // Expose protected methods for testing
        public string TestGetBlobLocation(BlobRequest item) => this.GetBlobLocation(item);

        public void TestValidateFile(BlobData item) => this.ValidateFile(item);

        #endregion
    }
}