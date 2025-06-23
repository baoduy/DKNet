# DKNet.Services.FileStorage

## How to use it?

There are 2 adapters provided by this service.

- Local file storage.
- Azure storage.

**1. Configuration sample as below:**

```json
{
  ...,
  
  "FileService": {
    "LocalFolder": {
      "RootFolder": "FileStorage"
    },
    "AzureStorage": {
      "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;EndpointSuffix=core.windows.net",
      "ContainerName": "WX"
    }
  }
}
```

**2. Service configuration as below:**

```csharp
 var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

 var service = new ServiceCollection()
                .AddLogging()
                .AddFileService()
                .AddAzureStorageAdapter(config)
                .AddLocalFolderAdapter(config)
                .BuildServiceProvider();

 var instance = service.GetRequiredService<IFileService>();
 ...
```

**3. Customize the adapter.**
- The adapter must be implemented of  interface `IFileAdapter`.
- Add adapter to the ServiceCollection.
```csharp
var service = new ServiceCollection()
                .AddLogging()
                .AddFileService()
                .AddFileAdapter<YourAdapter>()
                .BuildServiceProvider();
```
Note that if there are more than 1 Adapters provided. The file will be copied to all Adapters.
