using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using DKNet.Svc.BlobStorage.AwsS3;
using Svc.BlobStorage.Tests.Fixtures;

namespace Svc.BlobStorage.Tests;

public class S3BlobServiceTest(S3BlobServiceFixture fixture) : IClassFixture<S3BlobServiceFixture>
{
    #region Fields

    private readonly IBlobService _service = fixture.Service;

    #endregion

    #region Methods

    [Fact]
    public async Task CheckExistsAsyncReturnsFalseIfNotExists()
    {
        var fileName = $"not-exists-{Guid.NewGuid()}.txt";
        var exists = await _service.CheckExistsAsync(new BlobRequest(fileName) { Type = BlobTypes.File });
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task CheckExistsAsyncReturnsTrueIfExists()
    {
        var fileName = $"exists-check-{Guid.NewGuid()}.txt";
        var blob = new BlobDetails.BlobData(fileName, new BinaryData("exists"u8.ToArray()))
            { Overwrite = true, Type = BlobTypes.File };
        await _service.SaveAsync(blob);

        var exists = await _service.CheckExistsAsync(new BlobRequest(fileName) { Type = BlobTypes.File });
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsyncDeletesDirectory()
    {
        var dir = $"delete-dir-{Guid.NewGuid()}";
        var fileName = $"{dir}/file.txt";
        var blob = new BlobDetails.BlobData(fileName, new BinaryData("bye"u8.ToArray()))
            { Overwrite = true, Type = BlobTypes.File };
        await _service.SaveAsync(blob);
        var deleted = await _service.DeleteAsync(new BlobRequest(dir) { Type = BlobTypes.Directory });
        deleted.ShouldBeTrue();
        var items = new List<BlobDetails.BlobResult>();
        await foreach (var item in _service.ListItemsAsync(new BlobRequest(dir) { Type = BlobTypes.Directory }))
            items.Add(item);

        items.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteAsyncDeletesFile()
    {
        var fileName = $"delete-{Guid.NewGuid()}.txt";
        var blob = new BlobDetails.BlobData(fileName, new BinaryData("bye"u8.ToArray()))
            { Overwrite = true, Type = BlobTypes.File };
        await _service.SaveAsync(blob);
        var deleted = await _service.DeleteAsync(new BlobRequest(fileName) { Type = BlobTypes.File });
        deleted.ShouldBeTrue();
        var result = await _service.GetAsync(new BlobRequest(fileName) { Type = BlobTypes.File });
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsyncReturnsBlobDataResult()
    {
        var fileName = $"get-{Guid.NewGuid()}.txt";
        var blob = new BlobDetails.BlobData(fileName, new BinaryData("abc"u8.ToArray()))
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
    public async Task GetPublicAccessUrlReturnsUrl()
    {
        var fileName = $"public-{Guid.NewGuid()}.txt";
        var blob = new BlobDetails.BlobData(fileName, new BinaryData("public"u8.ToArray()))
            { Overwrite = true, Type = BlobTypes.File };
        await _service.SaveAsync(blob);
        var url = await _service.GetPublicAccessUrl(
            new BlobRequest(fileName) { Type = BlobTypes.File },
            TimeSpan.FromMinutes(5));
        url.ShouldNotBeNull();
        url.ShouldBeOfType<Uri>();
        url.ToString().ShouldContain(fileName);
    }

    [Fact]
    public async Task ListItemsAsyncListsFiles()
    {
        var dir = $"dir-{Guid.NewGuid()}";
        var fileName = $"{dir}/file.txt";
        var blob = new BlobDetails.BlobData(fileName, new BinaryData("data"u8.ToArray()))
            { Overwrite = true, Type = BlobTypes.File };
        await _service.SaveAsync(blob);
        var items = new List<BlobDetails.BlobResult>();
        await foreach (var item in _service.ListItemsAsync(new BlobRequest(dir) { Type = BlobTypes.Directory }))
            items.Add(item);

        items.ShouldContain(i => i.Name.Contains(fileName));
    }

    [Fact]
    public async Task SaveAsyncSavesFileAndOverwrites()
    {
        var fileName = $"test-{Guid.NewGuid()}.txt";
        var blob = new BlobDetails.BlobData(fileName, new BinaryData("world"u8.ToArray()))
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
        var blob = new BlobDetails.BlobData(fileName, new BinaryData("data"u8.ToArray()))
        {
            Overwrite = false,
            Type = BlobTypes.File
        };

        await _service.SaveAsync(blob);
        var action = () => _service.SaveAsync(blob);
        await action.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SavesFileAndList()
    {
        var fileName = $"new-file-{Guid.NewGuid()}.txt";
        var blob = new BlobDetails.BlobData(fileName, new BinaryData("world"u8.ToArray()))
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
    public async Task DeleteAsyncDeletesDirectoryWithMultipleKeys()
    {
        // Regression test for the per-key DeleteFolderAsync rewrite (one DeleteObjectAsync call per
        // key instead of a single batch DeleteObjectsAsync) — confirms it drops none of several keys.
        var dir = $"delete-dir-multi-{Guid.NewGuid()}";
        for (var i = 0; i < 5; i++)
        {
            var blob = new BlobDetails.BlobData($"{dir}/file{i}.txt", new BinaryData("bye"u8.ToArray()))
                { Overwrite = true, Type = BlobTypes.File };
            await _service.SaveAsync(blob);
        }

        var deleted = await _service.DeleteAsync(new BlobRequest(dir) { Type = BlobTypes.Directory });
        deleted.ShouldBeTrue();

        var items = new List<BlobDetails.BlobResult>();
        await foreach (var item in _service.ListItemsAsync(new BlobRequest(dir) { Type = BlobTypes.Directory }))
            items.Add(item);

        items.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteAsyncDeletesEmptyDirectoryWithoutThrowing()
    {
        var dir = $"delete-dir-empty-{Guid.NewGuid()}";

        var deleted = await _service.DeleteAsync(new BlobRequest(dir) { Type = BlobTypes.Directory });
        deleted.ShouldBeTrue();
    }

    [Fact]
    public async Task ListItemsAsync_ZeroByteAndOneByteFiles_AreClassifiedAsFileNotDirectory()
    {
        // Regression for C8: `obj.Size > 1` used to classify a 0- or 1-byte file as a directory.
        // Only a trailing '/' in the key is S3's actual directory-marker convention.
        var dir = $"tiny-files-{Guid.NewGuid()}";
        await _service.SaveAsync(new BlobDetails.BlobData($"{dir}/empty.txt", new BinaryData([]))
            { Overwrite = true, Type = BlobTypes.File });
        await _service.SaveAsync(new BlobDetails.BlobData($"{dir}/one-byte.txt", new BinaryData("a"u8.ToArray()))
            { Overwrite = true, Type = BlobTypes.File });

        var items = new List<BlobDetails.BlobResult>();
        await foreach (var item in _service.ListItemsAsync(new BlobRequest(dir) { Type = BlobTypes.Directory }))
            items.Add(item);

        items.Count.ShouldBe(2);
        items.ShouldAllBe(i => i.Type == BlobTypes.File);
    }

    [Fact]
    public async Task ListItemsAsync_MoreThanOnePage_ReturnsAllObjectsAcrossPages()
    {
        // Regression for C8: ListItemsAsync used to read only the first ListObjectsV2 page and
        // silently drop the rest. S3BlobService does not set MaxKeys, so it always requests the
        // provider's default page size (1000 for both S3 and this Minio image) — there is no
        // smaller way to make the server truncate the first page, so this genuinely uploads more
        // than 1000 objects to force a real ContinuationToken round trip rather than merely
        // asserting happy-path behaviour on a single page.
        const int objectCount = 1001;
        var dir = $"paged-{Guid.NewGuid()}";

        await Parallel.ForEachAsync(
            Enumerable.Range(0, objectCount),
            new ParallelOptions { MaxDegreeOfParallelism = 32 },
            async (i, ct) =>
            {
                var blob = new BlobDetails.BlobData($"{dir}/file{i:D4}.txt", new BinaryData("x"u8.ToArray()))
                    { Overwrite = true, Type = BlobTypes.File };
                await _service.SaveAsync(blob, ct);
            });

        var items = new List<BlobDetails.BlobResult>();
        await foreach (var item in _service.ListItemsAsync(new BlobRequest(dir) { Type = BlobTypes.Directory }))
            items.Add(item);

        items.Count.ShouldBe(objectCount);
    }

    [Fact]
    public async Task DisposeReleasesUnderlyingClientAndIsIdempotent()
    {
        var service = new S3BlobService(Options.Create(fixture.Options), NullLogger<S3BlobService>.Instance);

        // Force the lazy AmazonS3Client to be created before disposing it.
        await service.CheckExistsAsync(new BlobRequest($"dispose-check-{Guid.NewGuid()}.txt"));

        Should.NotThrow(service.Dispose);
        Should.NotThrow(service.Dispose); // second call must be a no-op, not re-dispose a null client
    }

    #endregion
}