using System.Text;
using Svc.BlobStorage.Tests.Fixtures;

namespace Svc.BlobStorage.Tests;

public class S3BlobServiceTest(S3BlobServiceFixture fixture) : IClassFixture<S3BlobServiceFixture>
{
    private readonly IBlobService _service = fixture.Service;

    [Fact]
    public async Task SavesFileAndList()
    {
        var fileName = $"new-file-{Guid.NewGuid()}.txt";
        var blob = new BlobData(fileName, new BinaryData("world"u8.ToArray()))
        {
            Overwrite = false,
            Type = BlobTypes.File
        };
        var name = await _service.SaveAsync(blob);
        name.ShouldBe(fileName);

        var items = await _service.ListItemsAsync(new BlobRequest("")).ToListAsync();
        items.Count.ShouldBeGreaterThanOrEqualTo(1);
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
        var getResult = await _service.GetAsync(new BlobRequest(fileName) { Type = BlobTypes.File });
        getResult.ShouldNotBeNull();
        var content = Encoding.UTF8.GetString(getResult.Data.ToArray());
        content.ShouldBe("hello");
    }

    [Fact]
    public async Task SaveAsyncThrowsIfExistsAndNoOverwrite()
    {
        var fileName = $"exists-{Guid.NewGuid()}.txt";
        var blob = new BlobData(fileName, new BinaryData("data"u8.ToArray()))
        {
            Overwrite = false,
            Type = BlobTypes.File
        };

        await _service.SaveAsync(blob);
        var action = () => _service.SaveAsync(blob);
        await action.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetAsyncReturnsBlobDataResult()
    {
        var fileName = $"get-{Guid.NewGuid()}.txt";
        var blob = new BlobData(fileName, new BinaryData("abc"u8.ToArray()))
            { Overwrite = true, Type = BlobTypes.File };
        await _service.SaveAsync(blob);
        var result = await _service.GetAsync(new BlobRequest(fileName) { Type = BlobTypes.File });
        result.ShouldNotBeNull();
        result.Name.ShouldBe(fileName);
        Encoding.UTF8.GetString(result.Data.ToArray()).ShouldBe("abc");
        result.Type.ShouldBe(BlobTypes.File);
        result.Details.ShouldNotBeNull();
        result.Details.ContentLength.ShouldBe(3);
    }

    [Fact]
    public async Task GetAsyncReturnsNullIfNotFound()
    {
        var result =
            await _service.GetAsync(new BlobRequest($"notfound-{Guid.NewGuid()}.txt") { Type = BlobTypes.File });
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ListItemsAsyncListsFiles()
    {
        var dir = $"dir-{Guid.NewGuid()}";
        var fileName = $"{dir}/file.txt";
        var blob = new BlobData(fileName, new BinaryData("data"u8.ToArray()))
            { Overwrite = true, Type = BlobTypes.File };
        await _service.SaveAsync(blob);
        var items = new List<BlobResult>();
        await foreach (var item in _service.ListItemsAsync(new BlobRequest(dir) { Type = BlobTypes.Directory }))
            items.Add(item);

        items.ShouldContain(i => i.Name.Contains(fileName));
    }

    [Fact]
    public async Task DeleteAsyncDeletesFile()
    {
        var fileName = $"delete-{Guid.NewGuid()}.txt";
        var blob = new BlobData(fileName, new BinaryData("bye"u8.ToArray()))
            { Overwrite = true, Type = BlobTypes.File };
        await _service.SaveAsync(blob);
        var deleted = await _service.DeleteAsync(new BlobRequest(fileName) { Type = BlobTypes.File });
        deleted.ShouldBeTrue();
        var result = await _service.GetAsync(new BlobRequest(fileName) { Type = BlobTypes.File });
        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsyncDeletesDirectory()
    {
        var dir = $"delete-dir-{Guid.NewGuid()}";
        var fileName = $"{dir}/file.txt";
        var blob = new BlobData(fileName, new BinaryData("bye"u8.ToArray()))
            { Overwrite = true, Type = BlobTypes.File };
        await _service.SaveAsync(blob);
        var deleted = await _service.DeleteAsync(new BlobRequest(dir) { Type = BlobTypes.Directory });
        deleted.ShouldBeTrue();
        var items = new List<BlobResult>();
        await foreach (var item in _service.ListItemsAsync(new BlobRequest(dir) { Type = BlobTypes.Directory }))
            items.Add(item);

        items.ShouldBeEmpty();
    }

    [Fact]
    public async Task CheckExistsAsyncReturnsTrueIfExists()
    {
        var fileName = $"exists-check-{Guid.NewGuid()}.txt";
        var blob = new BlobData(fileName, new BinaryData("exists"u8.ToArray()))
            { Overwrite = true, Type = BlobTypes.File };
        await _service.SaveAsync(blob);

        var exists = await _service.CheckExistsAsync(new BlobRequest(fileName) { Type = BlobTypes.File });
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task CheckExistsAsyncReturnsFalseIfNotExists()
    {
        var fileName = $"not-exists-{Guid.NewGuid()}.txt";
        var exists = await _service.CheckExistsAsync(new BlobRequest(fileName) { Type = BlobTypes.File });
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task GetPublicAccessUrlReturnsUrl()
    {
        var fileName = $"public-{Guid.NewGuid()}.txt";
        var blob = new BlobData(fileName, new BinaryData("public"u8.ToArray()))
            { Overwrite = true, Type = BlobTypes.File };
        await _service.SaveAsync(blob);
        var url = await _service.GetPublicAccessUrl(new BlobRequest(fileName) { Type = BlobTypes.File },
            TimeSpan.FromMinutes(5));
        url.ShouldNotBeNull();
        url.ShouldBeOfType<Uri>();
        url.ToString().ShouldContain(fileName);
    }
}