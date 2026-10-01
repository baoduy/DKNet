using Svc.BlobStorage.Tests.Fixtures;

namespace Svc.BlobStorage.Tests;

/// <summary>
///     DRK-1898 R4: only Directory list requests get the "/" folder boundary; a File request lists its own key.
/// </summary>
public class AzureStorageListItemsFileRequestTests(AzureStorageBlobServiceFixture fixture)
    : IClassFixture<AzureStorageBlobServiceFixture>
{
    #region Fields

    private readonly IBlobService _adapter = fixture.Service;

    #endregion

    #region Methods

    [Fact]
    public async Task ListItemsAsync_File_KeepsPrefixWithoutTrailingSlash()
    {
        var name = $"file-list-{Guid.NewGuid()}/a.txt";
        await _adapter.SaveAsync(new BlobDetails.BlobData(name, BinaryData.FromString("test"))
            { ContentType = "text/plain" });

        var items = await _adapter.ListItemsAsync(new BlobRequest(name) { Type = BlobTypes.File }).ToListAsync();

        items.Select(i => i.Name).ShouldBe([name]);
    }

    #endregion
}
