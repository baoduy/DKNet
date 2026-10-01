using Svc.BlobStorage.Tests.Fixtures;

namespace Svc.BlobStorage.Tests;

/// <summary>
///     DRK-1898 root-delete guard. Kept in its own class, with its own <see cref="S3BlobServiceFixture" /> (and so its
///     own Minio container), because without the guard these calls wipe the whole bucket — they must never be able to
///     delete another test class's data.
/// </summary>
public class S3BlobServiceRootDeleteTests(S3BlobServiceFixture fixture) : IClassFixture<S3BlobServiceFixture>
{
    #region Fields

    private readonly IBlobService _service = fixture.Service;

    #endregion

    #region Methods

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    public async Task DeleteAsync_EmptyName_Throws(string name)
    {
        var seeded = $"root-guard-{Guid.NewGuid()}.txt";
        await _service.SaveAsync(new BlobDetails.BlobData(seeded, new BinaryData("keep"u8.ToArray()))
            { Overwrite = true, Type = BlobTypes.File });

        // No explicit Type: an extension-less name, including "" and "/", defaults to Directory.
        var ex = await Should.ThrowAsync<ArgumentException>(() => _service.DeleteAsync(new BlobRequest(name)));

        (await _service.CheckExistsAsync(new BlobRequest(seeded) { Type = BlobTypes.File })).ShouldBeTrue();
        ex.ParamName.ShouldBe("blob");
    }

    #endregion
}
