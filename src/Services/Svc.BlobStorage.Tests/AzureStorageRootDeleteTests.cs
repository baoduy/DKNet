using Svc.BlobStorage.Tests.Fixtures;

namespace Svc.BlobStorage.Tests;

/// <summary>
///     DRK-1898 R5 root-delete guard for Azure. Kept in its own class, with its own
///     <see cref="AzureStorageBlobServiceFixture" /> (and so its own Azurite container), so a missing guard can never
///     delete another test class's data.
/// </summary>
public class AzureStorageRootDeleteTests(AzureStorageBlobServiceFixture fixture)
    : IClassFixture<AzureStorageBlobServiceFixture>
{
    #region Fields

    private readonly IBlobService _adapter = fixture.Service;

    #endregion

    #region Methods

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    public async Task DeleteAsync_EmptyName_Throws(string name)
    {
        var seeded = $"root-guard-{Guid.NewGuid()}.txt";
        await _adapter.SaveAsync(new BlobDetails.BlobData(seeded, BinaryData.FromString("keep"))
            { ContentType = "text/plain" });

        // No explicit Type: an extension-less name, including "" and "/", defaults to Directory.
        var ex = await Should.ThrowAsync<ArgumentException>(() => _adapter.DeleteAsync(new BlobRequest(name)));

        (await _adapter.CheckExistsAsync(new BlobRequest(seeded) { Type = BlobTypes.File })).ShouldBeTrue();
        ex.ParamName.ShouldBe("blob");
    }

    #endregion
}
