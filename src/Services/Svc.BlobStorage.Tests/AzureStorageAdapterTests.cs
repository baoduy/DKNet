using DKNet.Svc.BlobStorage.AzureStorage;

namespace Svc.BlobStorage.Tests;

public class AzureStorageBlobServiceTest
{
    private IBlobService _adapter;

    [OneTimeSetUp]
    public void Setup()
    {
        var azureContainer = new AzuriteBuilder()
            .WithCommand("--skipApiVersionCheck")
            .WithPortBinding(10000)
            .WithAutoRemove(true)
            .Build();

        azureContainer.StartAsync().GetAwaiter().GetResult();

        var config = new ConfigurationBuilder()
            //.AddJsonFile("appsettings.json")
            .AddInMemoryCollection(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "BlobService:AzureStorage:ConnectionString", "UseDevelopmentStorage=true" },
                { "BlobService:AzureStorage:ContainerName", "test" },
            })
            .Build();

        var service =
            new ServiceCollection()
                .AddLogging()
                .AddSingleton<IBlobService, AzureStorageBlobService>()
                .AddAzureStorageAdapter(config)
                .BuildServiceProvider();

        _adapter = service.GetRequiredService<IBlobService>();
    }

    [Test]
    [Order(0)]
    public async Task SaveNewFile()
    {
        var file = BinaryData.FromBytes(await File.ReadAllBytesAsync("TestData/log.txt"));
        await _adapter.SaveAsync(new BlobData("log.txt",file)
        {
            ContentType = "text/plain",
        });

        (await _adapter.CheckExistsAsync(new BlobRequest("log.txt") { Type = BlobTypes.File }
        )).ShouldBeTrue();
    }

    [Test]
    [Order(1)]
    public async Task GetPublicUrl()
    {
        var uri = await _adapter.GetPublicAccessUrl(new BlobRequest("log.txt") { Type = BlobTypes.File });
        uri.ShouldNotBeNull();
    }

    [Test]
    [Order(1)]
    public async Task SaveExistedFile()
    {
        var file = BinaryData.FromBytes(await File.ReadAllBytesAsync("TestData/log.txt"));
        var action = () => _adapter.SaveAsync(new BlobData("log.txt", file)
        {
            ContentType = "text/plain",
        });

        await action.ShouldThrowAsync<Exception>();
    }

    [Test]
    [Order(1)]
    public async Task SaveExistedWithOverWriteFile()
    {
        var file = BinaryData.FromBytes(await File.ReadAllBytesAsync("TestData/log.txt"));
        var action = () => _adapter.SaveAsync(new BlobData("log.txt", file)
        {
            ContentType = "text/plain",
            Overwrite = true,
        });

        await action.ShouldNotThrowAsync();
    }

    [Test]
    [Order(2)]
    public async Task GetFile()
    {
        var oldfile = BinaryData.FromBytes(await File.ReadAllBytesAsync("TestData/log.txt"));
        var file = await _adapter.GetAsync(new BlobRequest("log.txt") { Type = BlobTypes.File });

        oldfile.ToString().ShouldBe(file!.Data.ToString());
    }

    [Test]
    [Order(2)]
    public async Task ListFile()
    {
        (await _adapter.ListItemsAsync(new BlobRequest("/") { Type = BlobTypes.Directory }).ToListAsync()).Count
            .ShouldBeGreaterThanOrEqualTo(1);
    }

    [Test]
    [Order(3)]
    public async Task DeleteFileShouldReturnTrueWhenFileExists()
    {
        var file = BinaryData.FromString("test");
        await _adapter.SaveAsync(new BlobData("delete-me.txt", file) { ContentType = "text/plain" });
        var result = await _adapter.DeleteAsync(new BlobRequest("delete-me.txt") { Type = BlobTypes.File });
        result.ShouldBeTrue();
    }

    [Test]
    [Order(4)]
    public async Task DeleteFileShouldReturnTrueWhenFileDoesNotExist()
    {
        var result = await _adapter.DeleteAsync(new BlobRequest("not-exist.txt") { Type = BlobTypes.File });
        result.ShouldBeFalse();
    }

    [Test]
    [Order(5)]
    public async Task DeleteFolderShouldReturnTrueWhenFolderExists()
    {
        var file = BinaryData.FromString("test");
        await _adapter.SaveAsync(new BlobData("folder1/file1.txt", file) { ContentType = "text/plain" });
        var result = await _adapter.DeleteAsync(new BlobRequest("folder1") { Type = BlobTypes.Directory });
        result.ShouldBeTrue();
    }

    [Test]
    [Order(6)]
    public async Task CheckExistsAsyncShouldReturnFalseWhenBlobDoesNotExist()
    {
        var exists = await _adapter.CheckExistsAsync(new BlobRequest("not-exist.txt") { Type = BlobTypes.File });
        exists.ShouldBeFalse();
    }

    [Test]
    [Order(7)]
    public async Task ListItemsAsyncShouldReturnEmptyWhenNoItems()
    {
        var items = await _adapter.ListItemsAsync(new BlobRequest("empty-folder") { Type = BlobTypes.Directory })
            .ToListAsync();
        items.Count.ShouldBe(0);
    }
}