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
        var blobData = new BlobDetails.BlobData("test.TXT", data);

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
        var blobData = new BlobDetails.BlobData("verylongfilename.txt", data);

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
        var blobData = new BlobDetails.BlobData("test.txt", data);

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
        var blobData = new BlobDetails.BlobData("test.pdf", data);

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
        var blobData = new BlobDetails.BlobData("testfile", data);

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
        var blobData = new BlobDetails.BlobData("very_long_file_name_that_exceeds_typical_limits.txt", data);

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
        var blobData = new BlobDetails.BlobData("test.txt", data);

        // Act & Assert
        Should.NotThrow(() => service.TestValidateFile(blobData));
    }

    [Fact]
    public void ValidateFile_Stream_WithInvalidExtension_ShouldThrowFileLoadException()
    {
        // Arrange — the stream overload shares name/extension validation with the BinaryData overload.
        var options = new BlobServiceOptions { IncludedExtensions = [".txt"] };
        var service = new TestBlobService(options);
        var blobData = new BlobDetails.BlobStreamData("test.pdf", new MemoryStream("test"u8.ToArray()));

        // Act & Assert
        var exception = Should.Throw<FileLoadException>(() => service.TestValidateFile(blobData));
        exception.Message.ShouldBe("File extension is invalid.");
    }

    [Fact]
    public void ValidateFile_Stream_SeekableWithinLimit_ShouldReturnSameStreamInstance()
    {
        // Arrange — a seekable stream's size is known upfront, so no wrapping is needed.
        var options = new BlobServiceOptions { MaxFileSizeInMb = 1 };
        var service = new TestBlobService(options);
        var stream = new MemoryStream("test content"u8.ToArray());
        var blobData = new BlobDetails.BlobStreamData("test.txt", stream);

        // Act
        var result = service.TestValidateFile(blobData);

        // Assert
        result.ShouldBeSameAs(stream);
    }

    [Fact]
    public void ValidateFile_Stream_SeekableOversized_ShouldThrowImmediatelyWithoutReading()
    {
        // Arrange — size is checked via Stream.Length before a single byte is read.
        var options = new BlobServiceOptions { MaxFileSizeInMb = 1 };
        var service = new TestBlobService(options);
        var stream = new MemoryStream(new byte[2 * 1_000_000]);
        var blobData = new BlobDetails.BlobStreamData("test.txt", stream);

        // Act & Assert
        var exception = Should.Throw<FileLoadException>(() => service.TestValidateFile(blobData));
        exception.Message.ShouldBe("File size is invalid.");
        stream.Position.ShouldBe(0); // proves it never read the stream to measure it
    }

    [Fact]
    public async Task ValidateFile_Stream_NonSeekableWithinLimit_ShouldReadFullyWithoutThrowing()
    {
        // Arrange — a non-seekable source can't report Length, so the limit is enforced while copying.
        var options = new BlobServiceOptions { MaxFileSizeInMb = 1 };
        var service = new TestBlobService(options);
        var payload = "test content"u8.ToArray();
        var blobData = new BlobDetails.BlobStreamData("test.txt", new NonSeekableStream(payload));

        // Act
        var wrapped = service.TestValidateFile(blobData);
        using var destination = new MemoryStream();
        await wrapped.CopyToAsync(destination);

        // Assert
        destination.ToArray().ShouldBe(payload);
    }

    [Fact]
    public async Task ValidateFile_Stream_NonSeekableOversized_ShouldThrowWhileCopyingRatherThanSkipTheCheck()
    {
        // Arrange — length is unknowable upfront for a non-seekable stream; the limit must not be silently
        // skipped, so the wrapper aborts once the running byte count exceeds the ceiling.
        var options = new BlobServiceOptions { MaxFileSizeInMb = 1 };
        var service = new TestBlobService(options);
        var payload = new byte[2 * 1_000_000];
        var blobData = new BlobDetails.BlobStreamData("test.txt", new NonSeekableStream(payload));

        // Act
        var wrapped = service.TestValidateFile(blobData);

        // Assert
        using var destination = new MemoryStream();
        var exception = await Should.ThrowAsync<FileLoadException>(() => wrapped.CopyToAsync(destination));
        exception.Message.ShouldBe("File size is invalid.");
    }

    /// <summary>
    ///     A stream that reports <see cref="CanSeek" /> as <c>false</c> and throws on <see cref="Length" />, to
    ///     exercise the non-seekable branch of <c>BlobService.ValidateFile(BlobDetails.BlobStreamData)</c> the way
    ///     a live network response stream (S3/Azure) would behave.
    /// </summary>
    private sealed class NonSeekableStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    #endregion

    private class TestBlobService(BlobServiceOptions options) : BlobService(options)
    {
        #region Methods

        public override Task<bool> CheckExistsAsync(BlobRequest blob, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public override Task<bool> DeleteAsync(BlobRequest blob, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public override Task<BlobDetails.BlobDataResult?>
            GetAsync(BlobRequest blob, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public override Task<Uri> GetPublicAccessUrl(
            BlobRequest blob,
            TimeSpan? expiresFromNow = null,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public override async IAsyncEnumerable<BlobDetails.BlobResult> ListItemsAsync(
            BlobRequest blob,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Return empty enumerable for testing
            await Task.CompletedTask;
            yield break;
        }

        public override Task<string> SaveAsync(BlobDetails.BlobData blob,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        // Expose protected methods for testing
        public string TestGetBlobLocation(BlobRequest item) => GetBlobLocation(item);

        public void TestValidateFile(BlobDetails.BlobData item) => ValidateFile(item);

        public Stream TestValidateFile(BlobDetails.BlobStreamData item) => ValidateFile(item);

        #endregion
    }
}