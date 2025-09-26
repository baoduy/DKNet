namespace Svc.BlobStorage.Tests.Fixtures;

public sealed class LocalBlobServiceFixture : IDisposable
{
    public LocalBlobServiceFixture()
    {
        TestRoot = Path.Combine(Directory.GetCurrentDirectory(), "Test-Folder");

        if (Directory.Exists(TestRoot))
            Directory.Delete(TestRoot, true);
        Directory.CreateDirectory(TestRoot);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                (StringComparer.OrdinalIgnoreCase)
                {
                    { "BlobStorage:LocalFolder:RootFolder", "Test-Folder" }
                })
            .Build();

        var serviceCollection = new ServiceCollection()
            .AddLogging()
            .AddLocalDirectoryBlobService(config);

        var serviceProvider = serviceCollection.BuildServiceProvider();
        Service = serviceProvider.GetRequiredService<IBlobService>();
    }

    public IBlobService Service { get; }
    public string TestRoot { get; }

    public void Dispose()
    {
        if (Directory.Exists(TestRoot)) Directory.Delete(TestRoot, true);
        GC.SuppressFinalize(this);
    }
}