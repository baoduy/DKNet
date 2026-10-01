using Svc.BlobStorage.Tests.Fixtures;

namespace Svc.BlobStorage.Tests;

/// <summary>
///     DRK-1898 R5: an Azure folder delete removes exactly the blobs under <c>&lt;f&gt;/</c>, nested ones included.
/// </summary>
public class AzureStorageFolderDeleteTests(AzureStorageBlobServiceFixture fixture)
    : IClassFixture<AzureStorageBlobServiceFixture>
{
    #region Fields

    private readonly IBlobService _adapter = fixture.Service;

    #endregion

    #region Methods

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    public async Task DeleteAsync_Folder_DeletesNestedAndKeepsSiblingsWithSharedPrefix(string suffix)
    {
        var folder = $"shared-prefix-delete-{Guid.NewGuid()}";
        await SaveTextAsync($"{folder}/a.txt");
        await SaveTextAsync($"{folder}/sub/c.txt");
        await SaveTextAsync($"{folder}.pdf");
        await SaveTextAsync($"{folder}-archive/b.txt");

        await _adapter.DeleteAsync(new BlobRequest(folder + suffix) { Type = BlobTypes.Directory });

        (await ExistsAsync($"{folder}/a.txt")).ShouldBeFalse();
        (await ExistsAsync($"{folder}/sub/c.txt")).ShouldBeFalse();
        (await ExistsAsync($"{folder}.pdf")).ShouldBeTrue();
        (await ExistsAsync($"{folder}-archive/b.txt")).ShouldBeTrue();
    }

    private Task<bool> ExistsAsync(string name) =>
        _adapter.CheckExistsAsync(new BlobRequest(name) { Type = BlobTypes.File });

    private Task<string> SaveTextAsync(string name) =>
        _adapter.SaveAsync(new BlobDetails.BlobData(name, BinaryData.FromString("data"))
            { ContentType = "text/plain" });

    #endregion
}
