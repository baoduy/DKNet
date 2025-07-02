using Svc.BlobStorage.Tests.Fixtures;

namespace Svc.BlobStorage.Tests;

public class LocalBlobStorageTest : IClassFixture<LocalBlobServiceFixture>
{
    private readonly IBlobService _service;
    private readonly string _testRoot;

    public LocalBlobStorageTest(LocalBlobServiceFixture fixture)
    {
        _service = fixture.Service;
        _testRoot = fixture.TestRoot;
    }

    [Fact]
    public async Task SaveAsyncSavesFileAndOverwrites()
    {
        var fileName = $"test-{Guid.NewGuid()}.txt";
        var blob = new BlobData(fileName, new BinaryData("world"u8.ToArray()))
        {
            Overwrite = false,
            Type = BlobTypes.File
        };

        var name = await _service.SaveAsync(blob);
        name.ShouldBe(fileName);

        var newBlob = blob with { Overwrite = true, Data = new BinaryData("hello"u8.ToArray()) };

        await _service.SaveAsync(newBlob);
        var content = await File.ReadAllTextAsync(Path.Combine(_testRoot, fileName));
        content.ShouldBe("hello");
    }

    [Fact]
    public async Task SaveAsyncThrowsIfExistsAndNoOverwrite()
    {
        var filePath = Path.Combine(_testRoot, "exists.txt");
        await File.WriteAllTextAsync(filePath, "abc");

        var blob = new BlobData("exists.txt", new BinaryData("data"u8.ToArray()))
        {
            Overwrite = false,
            Type = BlobTypes.File
        };

        var action = () => _service.SaveAsync(blob);
        await action.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetAsyncReturnsBlobDataResult()
    {
        var filePath = Path.Combine(_testRoot, "get.txt");
        await File.WriteAllTextAsync(filePath, "abc");

        var blob = new BlobRequest("get.txt") { Type = BlobTypes.File };
        var result = await _service.GetAsync(blob);

        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetAsyncThrowsIfNotFound()
    {
        var blob = new BlobRequest("notfound.txt") { Type = BlobTypes.File };
        await Should.ThrowAsync<FileNotFoundException>(async () => await _service.GetAsync(blob));
    }

    [Fact]
    public async Task ListItemsAsyncListsFilesAndDirectories()
    {
        var dir = Path.Combine(_testRoot, "dir1");
        Directory.CreateDirectory(dir);
        var file1 = Path.Combine(dir, "a.txt");
        var file2 = Path.Combine(dir, "b.txt");
        await File.WriteAllTextAsync(file1, "1");
        await File.WriteAllTextAsync(file2, "2");
        var subDir = Path.Combine(dir, "sub");
        Directory.CreateDirectory(subDir);
        var file3 = Path.Combine(subDir, "c.txt");
        await File.WriteAllTextAsync(file3, "3");

        var blob = new BlobRequest("dir1") { Type = BlobTypes.Directory };
        var items = new List<BlobResult>();

        await foreach (var item in _service.ListItemsAsync(blob))
        {
            items.Add(item);
        }

        items.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task ListItemsAsyncListsSingleFile()
    {
        var file = Path.Combine(_testRoot, "single.txt");
        await File.WriteAllTextAsync(file, "x");
        var blob = new BlobRequest("single.txt") { Type = BlobTypes.File };
        var items = new List<BlobResult>();
        await foreach (var item in _service.ListItemsAsync(blob))
        {
            items.Add(item);
        }

        items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DeleteAsyncDeletesFileAndDirectory()
    {
        var file = Path.Combine(_testRoot, "delete-me.txt");
        await File.WriteAllTextAsync(file, "bye");
        var blob = new BlobRequest("delete-me.txt") { Type = BlobTypes.File };
        var deleted = await _service.DeleteAsync(blob);
        deleted.ShouldBeTrue();
        File.Exists(file).ShouldBeFalse();

        var dir = Path.Combine(_testRoot, "delete-dir");
        Directory.CreateDirectory(dir);
        var dirBlob = new BlobRequest("delete-dir") { Type = BlobTypes.Directory };
        var dirDeleted = await _service.DeleteAsync(dirBlob);
        dirDeleted.ShouldBeTrue();
        Directory.Exists(dir).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsyncReturnsFalseForNonExistentFileOrDirectory()
    {
        var blob = new BlobRequest("no-file.txt") { Type = BlobTypes.File };
        var deleted = await _service.DeleteAsync(blob);
        deleted.ShouldBeFalse();

        var dirBlob = new BlobRequest("no-dir") { Type = BlobTypes.Directory };
        var dirDeleted = await _service.DeleteAsync(dirBlob);
        dirDeleted.ShouldBeFalse();
    }

    [Fact]
    public async Task CheckExistsAsyncReturnsCorrectly()
    {
        var file = Path.Combine(_testRoot, "exists-check.txt");
        await File.WriteAllTextAsync(file, "exists");
        var blob = new BlobRequest("exists-check.txt") { Type = BlobTypes.File };
        (await _service.CheckExistsAsync(blob)).ShouldBeTrue();

        var dir = Path.Combine(_testRoot, "exists-dir");
        Directory.CreateDirectory(dir);
        var dirBlob = new BlobRequest("exists-dir") { Type = BlobTypes.Directory };
        (await _service.CheckExistsAsync(dirBlob)).ShouldBeTrue();

        var missingBlob = new BlobRequest("missing.txt") { Type = BlobTypes.File };
        (await _service.CheckExistsAsync(missingBlob)).ShouldBeFalse();
    }

    [Fact]
    public void GetPublicAccessUrlThrowsNotSupportedException()
    {
        var blob = new BlobRequest("any.txt") { Type = BlobTypes.File };
        Should.Throw<NotSupportedException>(() => _service.GetPublicAccessUrl(blob));
    }
}