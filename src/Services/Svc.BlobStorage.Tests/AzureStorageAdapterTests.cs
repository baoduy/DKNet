using Azure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using DKNet.Svc.BlobStorage.AzureStorage;
using Svc.BlobStorage.Tests.Fixtures;

namespace Svc.BlobStorage.Tests;

public class AzureStorageBlobServiceTest(AzureStorageBlobServiceFixture fixture)
    : IClassFixture<AzureStorageBlobServiceFixture>
{
    #region Fields

    private readonly IBlobService _adapter = fixture.Service;

    #endregion

    #region Methods

    [Fact]
    public async Task CheckExistsAsyncShouldReturnFalseWhenBlobDoesNotExist()
    {
        var exists = await _adapter.CheckExistsAsync(new BlobRequest("not-exist.txt") { Type = BlobTypes.File });
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteFileShouldReturnTrueWhenFileDoesNotExist()
    {
        var result = await _adapter.DeleteAsync(new BlobRequest("not-exist.txt") { Type = BlobTypes.File });
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteFileShouldReturnTrueWhenFileExists()
    {
        var file = BinaryData.FromString("test");
        await _adapter.SaveAsync(new BlobDetails.BlobData("delete-me.txt", file) { ContentType = "text/plain" });
        var result = await _adapter.DeleteAsync(new BlobRequest("delete-me.txt") { Type = BlobTypes.File });
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteFolderShouldReturnTrueWhenFolderExists()
    {
        var file = BinaryData.FromString("test");
        await _adapter.SaveAsync(new BlobDetails.BlobData("folder1/file1.txt", file) { ContentType = "text/plain" });
        var result = await _adapter.DeleteAsync(new BlobRequest("folder1") { Type = BlobTypes.Directory });
        result.ShouldBeTrue();
        (await _adapter.CheckExistsAsync(new BlobRequest("folder1/file1.txt") { Type = BlobTypes.File }))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task GetFile()
    {
        var fileName = $"get-file-{Guid.NewGuid()}.txt";
        var oldfile = BinaryData.FromBytes(await File.ReadAllBytesAsync("TestData/log.txt"));

        // First save the file
        await _adapter.SaveAsync(
            new BlobDetails.BlobData(fileName, oldfile)
            {
                ContentType = "text/plain"
            });

        // Then get it back
        var file = await _adapter.GetAsync(new BlobRequest(fileName) { Type = BlobTypes.File });

        oldfile.ToString().ShouldBe(file!.Data.ToString());
    }

    [Fact]
    public async Task GetPublicUrl()
    {
        var fileName = $"public-url-{Guid.NewGuid()}.txt";
        var file = BinaryData.FromBytes(await File.ReadAllBytesAsync("TestData/log.txt"));
        await _adapter.SaveAsync(
            new BlobDetails.BlobData(fileName, file)
            {
                ContentType = "text/plain"
            });

        var uri = await _adapter.GetPublicAccessUrl(new BlobRequest(fileName) { Type = BlobTypes.File });
        uri.ShouldNotBeNull();
    }

    [Fact]
    public async Task ListFile() =>
        (await _adapter.ListItemsAsync(new BlobRequest("/") { Type = BlobTypes.Directory }).ToListAsync()).Count
        .ShouldBeGreaterThanOrEqualTo(1);

    [Fact]
    public async Task ListItemsAsync_MultipleFilesInFolder_ReturnsDistinctPerItemNames()
    {
        // Regression for C7: every result used to carry the searched-for prefix instead of its own name.
        var folder = $"list-distinct-{Guid.NewGuid()}";
        var file = BinaryData.FromString("test");
        await _adapter.SaveAsync(new BlobDetails.BlobData($"{folder}/a.txt", file) { ContentType = "text/plain" });
        await _adapter.SaveAsync(new BlobDetails.BlobData($"{folder}/b.txt", file) { ContentType = "text/plain" });

        var items = await _adapter.ListItemsAsync(new BlobRequest(folder) { Type = BlobTypes.Directory })
            .ToListAsync();

        items.Count.ShouldBe(2);
        items.Select(i => i.Name).Distinct().Count().ShouldBe(2);
        items.ShouldContain(i => i.Name == $"{folder}/a.txt");
        items.ShouldContain(i => i.Name == $"{folder}/b.txt");
    }

    [Fact]
    public async Task ListItemsAsync_Folder_DoesNotReturnSiblingWithSharedPrefix()
    {
        // DRK-1898: listing folder "f" must return only blobs under "f/", not "f.pdf" or "f-archive/...".
        var folder = $"shared-prefix-list-{Guid.NewGuid()}";
        var file = BinaryData.FromString("test");
        await _adapter.SaveAsync(new BlobDetails.BlobData($"{folder}/a.txt", file) { ContentType = "text/plain" });
        await _adapter.SaveAsync(new BlobDetails.BlobData($"{folder}.pdf", file) { ContentType = "text/plain" });
        await _adapter.SaveAsync(
            new BlobDetails.BlobData($"{folder}-archive/b.txt", file) { ContentType = "text/plain" });

        var items = await _adapter.ListItemsAsync(new BlobRequest(folder) { Type = BlobTypes.Directory })
            .ToListAsync();

        items.Select(i => i.Name).ShouldBe([$"{folder}/a.txt"]);
    }

    [Fact]
    public async Task ListItemsAsyncShouldReturnEmptyWhenNoItems()
    {
        var items = await _adapter.ListItemsAsync(new BlobRequest("empty-folder") { Type = BlobTypes.Directory })
            .ToListAsync();
        items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task SaveExistedFile()
    {
        var fileName = $"exists-{Guid.NewGuid()}.txt";
        var file = BinaryData.FromBytes(await File.ReadAllBytesAsync("TestData/log.txt"));

        // First save the file
        await _adapter.SaveAsync(
            new BlobDetails.BlobData(fileName, file)
            {
                ContentType = "text/plain"
            });

        // Try to save again without overwrite - should fail
        var action = () => _adapter.SaveAsync(
            new BlobDetails.BlobData(fileName, file)
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
        await _adapter.SaveAsync(
            new BlobDetails.BlobData(fileName, file)
            {
                ContentType = "text/plain"
            });

        // Try to save again with overwrite - should succeed
        var action = () => _adapter.SaveAsync(
            new BlobDetails.BlobData(fileName, file)
            {
                ContentType = "text/plain",
                Overwrite = true
            });

        await action.ShouldNotThrowAsync();
    }

    [Fact]
    public async Task SaveNewFile()
    {
        var fileName = $"log-{Guid.NewGuid()}.txt";
        var file = BinaryData.FromBytes(await File.ReadAllBytesAsync("TestData/log.txt"));
        await _adapter.SaveAsync(
            new BlobDetails.BlobData(fileName, file)
            {
                ContentType = "text/plain"
            });

        (await _adapter.CheckExistsAsync(new BlobRequest(fileName) { Type = BlobTypes.File })).ShouldBeTrue();
    }

    [Fact]
    public async Task GetAsync_MissingBlob_ReturnsNullInsteadOfThrowing()
    {
        // Regression for P11: GetAsync used to call GetPropertiesAsync first, which throws
        // RequestFailedException(404) for a missing blob before the ExistsAsync check below it could
        // ever run. Collapsing to one DownloadContentAsync call inside a 404 catch fixes both the extra
        // round trips and this uncaught-exception gap.
        var result = await _adapter.GetAsync(new BlobRequest($"missing-{Guid.NewGuid()}.txt")
            { Type = BlobTypes.File });

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetClient_ConcurrentFirstCallers_BuildsContainerClientExactlyOnce()
    {
        // Regression for P11: the BlobContainerClient used to be built (and CreateIfNotExistsAsync run)
        // on every scoped instance, and even within one instance the null-check guard was not thread-safe.
        // BlobServiceClientFactory is an existing extensibility point on AzureStorageOptions, so it doubles
        // here as the observation seam: counting its invocations across many concurrent first callers on a
        // single instance proves the client build (and container-ensure) runs exactly once.
        var buildCount = 0;
        var options = new AzureStorageOptions
        {
            ContainerName = fixture.Options.ContainerName,
            BlobServiceClientFactory = _ =>
            {
                Interlocked.Increment(ref buildCount);
                return Task.FromResult(new BlobServiceClient(fixture.Options.ConnectionString));
            }
        };
        var service = new AzureStorageBlobService(Options.Create(options));

        await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(i => service.CheckExistsAsync(new BlobRequest($"concurrent-probe-{i}-{Guid.NewGuid()}.txt"))));

        buildCount.ShouldBe(1);
    }

    [Fact]
    public async Task SaveAsync_BlobDataWithExplicitContentType_GetAndListReturnCallerContentType()
    {
        // DRK-2486: the Azure adapter dropped the caller's ContentType and stored application/octet-stream.
        var name = $"content-type-data-{Guid.NewGuid()}/data.txt";

        await _adapter.SaveAsync(
            new BlobDetails.BlobData(name, BinaryData.FromString("{}")) { ContentType = "application/json" });

        (await ReadContentTypesAsync(name)).ShouldBe(("application/json", "application/json"));
    }

    [Fact]
    public async Task SaveAsync_BlobStreamDataWithExplicitContentType_GetAndListReturnCallerContentType()
    {
        // DRK-2486: same defect through the stream overload, which the BlobData overload delegates to.
        var name = $"content-type-stream-{Guid.NewGuid()}/data.txt";

        await _adapter.SaveAsync(
            new BlobDetails.BlobStreamData(name, new MemoryStream("{}"u8.ToArray()))
                { ContentType = "application/json" });

        (await ReadContentTypesAsync(name)).ShouldBe(("application/json", "application/json"));
    }

    [Fact]
    public async Task SaveAsync_OverwriteWithDifferentContentType_ReplacesContentAndContentType()
    {
        // DRK-2486 R3: an overwrite replaces both the content and the stored ContentType.
        var name = $"content-type-overwrite-{Guid.NewGuid()}/data.txt";
        await _adapter.SaveAsync(
            new BlobDetails.BlobData(name, BinaryData.FromString("first")) { ContentType = "text/plain" });
        (await ReadContentTypesAsync(name)).ShouldBe(("text/plain", "text/plain"));

        await _adapter.SaveAsync(
            new BlobDetails.BlobData(name, BinaryData.FromString("{}"))
                { ContentType = "application/json", Overwrite = true });

        (await ReadContentTypesAsync(name)).ShouldBe(("application/json", "application/json"));
        (await _adapter.GetAsync(new BlobRequest(name) { Type = BlobTypes.File }))!.Data.ToString().ShouldBe("{}");
    }

    [Theory]
    [InlineData("data.pdf", "application/pdf")]
    [InlineData("data.unknownext", "application/octet-stream")]
    public async Task SaveAsync_DefaultContentType_StoresExtensionContentType(string fileName, string expected)
    {
        var name = $"content-type-default-{Guid.NewGuid()}/{fileName}";

        await _adapter.SaveAsync(new BlobDetails.BlobData(name, BinaryData.FromString("x")));

        (await ReadContentTypesAsync(name)).ShouldBe((expected, expected));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SaveAsync_EmptyContentType_StoresAzureDefaultContentType(string? contentType)
    {
        // DRK-2486 R4: a null, empty or whitespace ContentType sends no header instead of an empty one.
        var name = $"content-type-empty-{Guid.NewGuid()}/data.txt";

        await _adapter.SaveAsync(
            new BlobDetails.BlobData(name, BinaryData.FromString("x")) { ContentType = contentType! });

        (await ReadContentTypesAsync(name)).ShouldBe(("application/octet-stream", "application/octet-stream"));
    }

    [Fact]
    public async Task SaveAsync_ExistingBlobWithoutOverwrite_ThrowsConflictAndKeepsFirstBlob()
    {
        // DRK-2486 R2: the Conditions rewrite keeps the 409 BlobAlreadyExists failure and leaves the blob untouched.
        var name = $"content-type-conflict-{Guid.NewGuid()}/data.txt";
        await _adapter.SaveAsync(
            new BlobDetails.BlobData(name, BinaryData.FromString("first")) { ContentType = "text/plain" });

        var ex = await Should.ThrowAsync<RequestFailedException>(() => _adapter.SaveAsync(
            new BlobDetails.BlobData(name, BinaryData.FromString("{}")) { ContentType = "application/json" }));

        ex.Status.ShouldBe(409);
        ex.ErrorCode.ShouldBe("BlobAlreadyExists");
        (await ReadContentTypesAsync(name)).ShouldBe(("text/plain", "text/plain"));
        (await _adapter.GetAsync(new BlobRequest(name) { Type = BlobTypes.File }))!.Data.ToString().ShouldBe("first");
    }

    /// <summary>
    ///     Reads a saved blob's ContentType back through both <see cref="IBlobService.GetAsync" /> and
    ///     <see cref="IBlobService.ListItemsAsync" />; <paramref name="name" /> must be the only blob in its folder.
    /// </summary>
    private async Task<(string? Get, string? List)> ReadContentTypesAsync(string name)
    {
        var got = await _adapter.GetAsync(new BlobRequest(name) { Type = BlobTypes.File });
        var folder = name[..name.LastIndexOf('/')];
        var listed = await _adapter.ListItemsAsync(new BlobRequest(folder) { Type = BlobTypes.Directory })
            .ToListAsync();

        return (got!.Details!.ContentType, listed.ShouldHaveSingleItem().Details!.ContentType);
    }

    #endregion
}