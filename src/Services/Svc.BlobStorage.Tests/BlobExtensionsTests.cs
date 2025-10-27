namespace Svc.BlobStorage.Tests;

public class BlobExtensionsTests
{
    #region Methods

    [Theory]
    [InlineData("TEST.TXT", "text/plain")]
    [InlineData("TEST.PNG", "image/png")]
    [InlineData("TEST.PDF", "application/pdf")]
    public void GetContentTypeByExtension_ShouldBeCaseInsensitive(string fileName, string expectedContentType)
    {
        // Act
        var result = fileName.GetContentTypeByExtension();

        // Assert
        result.ShouldBe(expectedContentType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetContentTypeByExtension_ShouldHandleInvalidInput(string fileName)
    {
        // Act
        var result = fileName.GetContentTypeByExtension();

        // Assert
        result.ShouldBe("application/octet-stream");
    }

    [Theory]
    [InlineData("test.txt", "text/plain")]
    [InlineData("test.csv", "text/csv")]
    [InlineData("test.json", "application/json")]
    [InlineData("test.xml", "application/xml")]
    [InlineData("test.html", "text/html")]
    [InlineData("test.htm", "text/html")]
    [InlineData("test.jpg", "image/jpeg")]
    [InlineData("test.jpeg", "image/jpeg")]
    [InlineData("test.png", "image/png")]
    [InlineData("test.gif", "image/gif")]
    [InlineData("test.bmp", "image/bmp")]
    [InlineData("test.pdf", "application/pdf")]
    [InlineData("test.zip", "application/zip")]
    [InlineData("test.tar", "application/x-tar")]
    [InlineData("test.gz", "application/gzip")]
    [InlineData("test.mp3", "audio/mpeg")]
    [InlineData("test.mp4", "video/mp4")]
    [InlineData("test.doc", "application/msword")]
    [InlineData("test.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("test.xls", "application/vnd.ms-excel")]
    [InlineData("test.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("test.unknown", "application/octet-stream")]
    [InlineData("test", "application/octet-stream")]
    public void GetContentTypeByExtension_ShouldReturnCorrectContentType(string fileName, string expectedContentType)
    {
        // Act
        var result = fileName.GetContentTypeByExtension();

        // Assert
        result.ShouldBe(expectedContentType);
    }

    [Fact]
    public void GetContentTypeByExtension_WithNullInput_ShouldThrowNullReferenceException()
    {
        // Arrange
        string fileName = null!;

        // Act & Assert
        Should.Throw<NullReferenceException>(() => fileName.GetContentTypeByExtension());
    }

    #endregion
}