using Svc.BlobStorage.Tests.Fixtures;

namespace Svc.BlobStorage.Tests;

public class AzureStorageBlobServiceTest(AzureStorageBlobServiceFixture fixture)
    : IClassFixture<AzureStorageBlobServiceFixture>
{
    private readonly IBlobService _adapter = fixture.Service;

    [Fact]
    public async Task SaveNewFile()
    {
        var fileName = $"log-{Guid.NewGuid()}.txt";
        var file = BinaryData.FromBytes(await File.ReadAllBytesAsync("TestData/log.txt"));
        await _adapter.SaveAsync(new BlobData(fileName, file)
        {
            ContentType = "text/plain"
        });

        (await _adapter.CheckExistsAsync(new BlobRequest(fileName) { Type = BlobTypes.File }
        )).ShouldBeTrue();
    }

    [Fact]
    public async Task GetPublicUrl()
    {
        var fileName = $"public-url-{Guid.NewGuid()}.txt";
        var file = BinaryData.FromBytes(await File.ReadAllBytesAsync("TestData/log.txt"));
        await _adapter.SaveAsync(new BlobData(fileName, file)
        {
            ContentType = "text/plain"
        });

        var uri = await _adapter.GetPublicAccessUrl(new BlobRequest(fileName) { Type = BlobTypes.File });
        uri.ShouldNotBeNull();
    }

    [Fact]
    public async Task SaveExistedFile()
    {
        var fileName = $"exists-{Guid.NewGuid()}.txt";
        var file = BinaryData.FromBytes(await File.ReadAllBytesAsync("TestData/log.txt"));

        // First save the file
        await _adapter.SaveAsync(new BlobData(fileName, file)
        {
            ContentType = "text/plain"
        });

        // Try to save again without overwrite - should fail
        var action = () => _adapter.SaveAsync(new BlobData(fileName, file)
        {
            ContentType = "text/plain"
        });

        await action.ShouldThrowAsync<Exception>();
    }

    [Fact]
    public async Task SaveExistedWithOverWriteFile()
    {
        var fileName = $"overwrite-{Guid.NewGuid()}.txt";
        var file = BinaryData.FromBytes(await File.ReadAllBytesAsync("TestData/log.txt"));

        // First save the file
        await _adapter.SaveAsync(new BlobData(fileName, file)
        {
            ContentType = "text/plain"
        });

        // Try to save again with overwrite - should succeed
        var action = () => _adapter.SaveAsync(new BlobData(fileName, file)
        {
            ContentType = "text/plain",
            Overwrite = true
        });

        await action.ShouldNotThrowAsync();
    }

    [Fact]
    public async Task GetFile()
    {
        var fileName = $"get-file-{Guid.NewGuid()}.txt";
        var oldfile = BinaryData.FromBytes(await File.ReadAllBytesAsync("TestData/log.txt"));

        // First save the file
        await _adapter.SaveAsync(new BlobData(fileName, oldfile)
        {
            ContentType = "text/plain"
        });

        // Then get it back
        var file = await _adapter.GetAsync(new BlobRequest(fileName) { Type = BlobTypes.File });

        oldfile.ToString().ShouldBe(file!.Data.ToString());
    }

    [Fact]
    public async Task ListFile() =>
        (await _adapter.ListItemsAsync(new BlobRequest("/") { Type = BlobTypes.Directory }).ToListAsync()).Count
        .ShouldBeGreaterThanOrEqualTo(1);

    [Fact]
    public async Task DeleteFileShouldReturnTrueWhenFileExists()
    {
        var file = BinaryData.FromString("test");
        await _adapter.SaveAsync(new BlobData("delete-me.txt", file) { ContentType = "text/plain" });
        var result = await _adapter.DeleteAsync(new BlobRequest("delete-me.txt") { Type = BlobTypes.File });
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteFileShouldReturnTrueWhenFileDoesNotExist()
    {
        var result = await _adapter.DeleteAsync(new BlobRequest("not-exist.txt") { Type = BlobTypes.File });
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteFolderShouldReturnTrueWhenFolderExists()
    {
        var file = BinaryData.FromString("test");
        await _adapter.SaveAsync(new BlobData("folder1/file1.txt", file) { ContentType = "text/plain" });
        var result = await _adapter.DeleteAsync(new BlobRequest("folder1") { Type = BlobTypes.Directory });
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task CheckExistsAsyncShouldReturnFalseWhenBlobDoesNotExist()
    {
        var exists = await _adapter.CheckExistsAsync(new BlobRequest("not-exist.txt") { Type = BlobTypes.File });
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task ListItemsAsyncShouldReturnEmptyWhenNoItems()
    {
        var items = await _adapter.ListItemsAsync(new BlobRequest("empty-folder") { Type = BlobTypes.Directory })
            .ToListAsync();
        items.Count.ShouldBe(0);
    }
}