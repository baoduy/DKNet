# DKNet.Svc.BlobStorage.AzureStorage

[![NuGet](https://img.shields.io/nuget/v/DKNet.Svc.BlobStorage.AzureStorage)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.AzureStorage/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Svc.BlobStorage.AzureStorage)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.AzureStorage/)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../../../LICENSE)

Azure Blob Storage implementation of the DKNet blob storage abstractions, providing seamless integration with Azure Storage services. This package offers production-ready blob storage capabilities with Azure-specific optimizations and features.

## Features

- **Azure Blob Storage Integration**: Direct integration with Azure Storage SDK
- **Full IBlobService Implementation**: Complete implementation of DKNet blob storage abstractions
- **Connection String Support**: Flexible connection string configuration
- **Container Management**: Automatic container creation and management
- **Blob Metadata**: Support for custom metadata and properties
- **Streaming Support**: Efficient streaming for large file operations
- **Error Handling**: Robust error handling with Azure-specific exceptions
- **Performance Optimized**: Leverages Azure Storage SDK optimizations

## Supported Frameworks

- .NET 9.0+
- Azure.Storage.Blobs 12.0+

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package DKNet.Svc.BlobStorage.AzureStorage
```

Or via Package Manager Console:

```powershell
Install-Package DKNet.Svc.BlobStorage.AzureStorage
```

## Quick Start

### Configuration Setup

```json
{
  "BlobService": {
    "AzureStorage": {
      "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net",
      "ContainerName": "mycontainer",
      "EnableMetadata": true,
      "MaxFileSize": 104857600,
      "GenerateUniqueNames": false,
      "PathPrefix": "uploads/"
    }
  }
}
```

### Service Registration

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    // Add Azure Storage blob service
    services.AddAzureStorageAdapter(configuration);
    
    // Or configure manually
    services.Configure<AzureStorageOptions>(options =>
    {
        options.ConnectionString = "your-connection-string";
        options.ContainerName = "your-container";
        options.EnableMetadata = true;
        options.MaxFileSize = 100 * 1024 * 1024; // 100MB
    });
}
```

### Basic Usage

```csharp
using DKNet.Svc.BlobStorage.Abstractions;

public class DocumentService
{
    private readonly IBlobService _blobService;

    public DocumentService(IBlobService blobService)
    {
        _blobService = blobService;
    }

    public async Task<string> UploadDocumentAsync(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        
        var blob = new BlobData
        {
            Name = $"documents/{Guid.NewGuid()}/{file.FileName}",
            ContentStream = stream,
            ContentType = file.ContentType,
            Metadata = new Dictionary<string, string>
            {
                ["OriginalFileName"] = file.FileName,
                ["UploadedAt"] = DateTime.UtcNow.ToString("O"),
                ["FileSize"] = file.Length.ToString()
            }
        };

        return await _blobService.SaveAsync(blob);
    }

    public async Task<Stream?> DownloadDocumentAsync(string blobName)
    {
        var request = new BlobRequest(blobName);
        var result = await _blobService.GetAsync(request);
        
        return result?.ContentStream;
    }
}
```

## Configuration

### Azure Storage Options

```csharp
public class AzureStorageOptions : BlobServiceOptions
{
    public string ConnectionString { get; set; } = null!;
    public string ContainerName { get; set; } = null!;
    
    // Inherited from BlobServiceOptions
    public bool EnableMetadata { get; set; } = true;
    public long MaxFileSize { get; set; } = 50 * 1024 * 1024; // 50MB
    public string[] AllowedExtensions { get; set; } = Array.Empty<string>();
    public string[] BlockedExtensions { get; set; } = { ".exe", ".bat", ".cmd" };
    public bool GenerateUniqueNames { get; set; } = false;
    public string PathPrefix { get; set; } = string.Empty;
}
```

### Connection String Formats

```csharp
// Storage account connection string
"DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net"

// With custom endpoint
"DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;BlobEndpoint=https://myaccount.blob.core.windows.net/"

// Using SAS token
"BlobEndpoint=https://myaccount.blob.core.windows.net/;SharedAccessSignature=sv=2020-02-10&ss=b&srt=sco&sp=rwdlacx&se=2021-12-31T23:59:59Z&st=2021-01-01T00:00:00Z&spr=https&sig=signature"

// Development (Azurite emulator)
"UseDevelopmentStorage=true"
```

### Environment-Specific Configuration

```csharp
// Production configuration
public void ConfigureProductionServices(IServiceCollection services, IConfiguration configuration)
{
    services.Configure<AzureStorageOptions>(options =>
    {
        options.ConnectionString = configuration.GetConnectionString("AzureStorage");
        options.ContainerName = "production-files";
        options.EnableMetadata = true;
        options.MaxFileSize = 500 * 1024 * 1024; // 500MB for production
        options.AllowedExtensions = new[] { ".pdf", ".jpg", ".png", ".docx" };
    });
    
    services.AddAzureStorageAdapter(configuration);
}

// Development configuration
public void ConfigureDevelopmentServices(IServiceCollection services, IConfiguration configuration)
{
    services.Configure<AzureStorageOptions>(options =>
    {
        options.ConnectionString = "UseDevelopmentStorage=true"; // Azurite
        options.ContainerName = "dev-files";
        options.EnableMetadata = true;
        options.MaxFileSize = 10 * 1024 * 1024; // 10MB for development
    });
    
    services.AddAzureStorageAdapter(configuration);
}
```

## API Reference

### AzureStorageBlobService

Implements `IBlobService` with Azure Blob Storage backend:

- `SaveAsync(BlobData, CancellationToken)` - Upload blob to Azure Storage
- `GetAsync(BlobRequest, CancellationToken)` - Download blob from Azure Storage
- `GetItemAsync(BlobRequest, CancellationToken)` - Get blob metadata only
- `ListItemsAsync(BlobRequest, CancellationToken)` - List blobs in container
- `DeleteAsync(BlobRequest, CancellationToken)` - Delete blob from Azure Storage
- `ExistsAsync(BlobRequest, CancellationToken)` - Check if blob exists

### AzureStorageOptions

Configuration class extending `BlobServiceOptions`:

- `ConnectionString` - Azure Storage connection string
- `ContainerName` - Target container name
- Plus all base blob service options

### Setup Extensions

- `AddAzureStorageAdapter(IConfiguration)` - Register Azure Storage implementation

## Advanced Usage

### Custom Metadata and Properties

```csharp
public class ImageService
{
    private readonly IBlobService _blobService;

    public ImageService(IBlobService blobService)
    {
        _blobService = blobService;
    }

    public async Task<string> UploadImageAsync(byte[] imageData, string fileName, ImageMetadata metadata)
    {
        var blob = new BlobData
        {
            Name = $"images/{DateTime.UtcNow:yyyy/MM/dd}/{fileName}",
            Content = imageData,
            ContentType = "image/jpeg",
            Metadata = new Dictionary<string, string>
            {
                ["Width"] = metadata.Width.ToString(),
                ["Height"] = metadata.Height.ToString(),
                ["Format"] = metadata.Format,
                ["Quality"] = metadata.Quality.ToString(),
                ["Camera"] = metadata.CameraModel ?? "Unknown",
                ["Location"] = metadata.GpsLocation ?? "Unknown"
            }
        };

        return await _blobService.SaveAsync(blob);
    }

    public async Task<ImageInfo?> GetImageInfoAsync(string imageName)
    {
        var request = new BlobRequest(imageName);
        var result = await _blobService.GetItemAsync(request);

        if (result?.Details == null)
            return null;

        return new ImageInfo
        {
            Name = result.Name,
            Size = result.Details.ContentLength,
            ContentType = result.Details.ContentType,
            LastModified = result.Details.LastModified,
            Width = GetMetadataValue(result.Metadata, "Width"),
            Height = GetMetadataValue(result.Metadata, "Height"),
            Format = GetMetadataValue(result.Metadata, "Format")
        };
    }

    private static string GetMetadataValue(IDictionary<string, string>? metadata, string key)
    {
        return metadata?.TryGetValue(key, out var value) == true ? value : "Unknown";
    }
}
```

### Blob Lifecycle Management

```csharp
public class BlobLifecycleService
{
    private readonly IBlobService _blobService;
    private readonly ILogger<BlobLifecycleService> _logger;

    public BlobLifecycleService(IBlobService blobService, ILogger<BlobLifecycleService> logger)
    {
        _blobService = blobService;
        _logger = logger;
    }

    public async Task ArchiveOldBlobsAsync(TimeSpan maxAge)
    {
        var cutoffDate = DateTime.UtcNow - maxAge;
        var request = new BlobRequest("") { Type = BlobTypes.Directory };

        await foreach (var blob in _blobService.ListItemsAsync(request))
        {
            if (blob.Details?.LastModified < cutoffDate)
            {
                _logger.LogInformation("Archiving old blob: {BlobName}", blob.Name);
                
                // Move to archive container or delete
                await _blobService.DeleteAsync(new BlobRequest(blob.Name));
            }
        }
    }

    public async Task<long> CalculateStorageUsageAsync()
    {
        long totalSize = 0;
        var request = new BlobRequest("") { Type = BlobTypes.Directory };

        await foreach (var blob in _blobService.ListItemsAsync(request))
        {
            totalSize += blob.Details?.ContentLength ?? 0;
        }

        return totalSize;
    }
}
```

### Batch Operations

```csharp
public class BatchBlobService
{
    private readonly IBlobService _blobService;
    private readonly ILogger<BatchBlobService> _logger;

    public BatchBlobService(IBlobService blobService, ILogger<BatchBlobService> logger)
    {
        _blobService = blobService;
        _logger = logger;
    }

    public async Task<List<string>> UploadMultipleFilesAsync(IEnumerable<IFormFile> files)
    {
        var uploadTasks = files.Select(async file =>
        {
            try
            {
                using var stream = file.OpenReadStream();
                var blob = new BlobData
                {
                    Name = $"batch-upload/{Guid.NewGuid()}/{file.FileName}",
                    ContentStream = stream,
                    ContentType = file.ContentType
                };

                var location = await _blobService.SaveAsync(blob);
                _logger.LogDebug("Successfully uploaded {FileName} to {Location}", file.FileName, location);
                return location;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload {FileName}", file.FileName);
                return null;
            }
        });

        var results = await Task.WhenAll(uploadTasks);
        return results.Where(r => r != null).Cast<string>().ToList();
    }

    public async Task DeleteBlobsByPrefixAsync(string prefix)
    {
        var request = new BlobRequest(prefix) { Type = BlobTypes.Directory };
        var deleteTasks = new List<Task>();

        await foreach (var blob in _blobService.ListItemsAsync(request))
        {
            var deleteTask = DeleteSafelyAsync(blob.Name);
            deleteTasks.Add(deleteTask);

            // Process in batches to avoid overwhelming the service
            if (deleteTasks.Count >= 10)
            {
                await Task.WhenAll(deleteTasks);
                deleteTasks.Clear();
            }
        }

        if (deleteTasks.Any())
        {
            await Task.WhenAll(deleteTasks);
        }
    }

    private async Task DeleteSafelyAsync(string blobName)
    {
        try
        {
            await _blobService.DeleteAsync(new BlobRequest(blobName));
            _logger.LogDebug("Successfully deleted {BlobName}", blobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete {BlobName}", blobName);
        }
    }
}
```

### Integration with ASP.NET Core

```csharp
[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly IBlobService _blobService;
    private readonly ILogger<FilesController> _logger;

    public FilesController(IBlobService blobService, ILogger<FilesController> logger)
    {
        _blobService = blobService;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile([FromForm] FileUploadRequest request)
    {
        if (request.File == null || request.File.Length == 0)
            return BadRequest("No file uploaded");

        try
        {
            using var stream = request.File.OpenReadStream();
            var blob = new BlobData
            {
                Name = $"uploads/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}/{request.File.FileName}",
                ContentStream = stream,
                ContentType = request.File.ContentType,
                Metadata = new Dictionary<string, string>
                {
                    ["OriginalFileName"] = request.File.FileName,
                    ["UploadedBy"] = User.Identity?.Name ?? "Anonymous",
                    ["Category"] = request.Category ?? "General"
                }
            };

            var location = await _blobService.SaveAsync(blob);
            
            return Ok(new { 
                Location = location, 
                FileName = request.File.FileName,
                Size = request.File.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file {FileName}", request.File.FileName);
            return StatusCode(500, "Upload failed");
        }
    }

    [HttpGet("download/{*blobName}")]
    public async Task<IActionResult> DownloadFile(string blobName)
    {
        try
        {
            var request = new BlobRequest(blobName);
            var result = await _blobService.GetAsync(request);

            if (result == null)
                return NotFound();

            var fileName = Path.GetFileName(blobName);
            return File(result.ContentStream, result.ContentType ?? "application/octet-stream", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file {BlobName}", blobName);
            return StatusCode(500, "Download failed");
        }
    }

    [HttpGet("list")]
    public async Task<IActionResult> ListFiles([FromQuery] string? prefix = null)
    {
        try
        {
            var request = new BlobRequest(prefix ?? "") { Type = BlobTypes.Directory };
            var files = new List<FileInfo>();

            await foreach (var blob in _blobService.ListItemsAsync(request))
            {
                files.Add(new FileInfo
                {
                    Name = blob.Name,
                    Size = blob.Details?.ContentLength ?? 0,
                    ContentType = blob.Details?.ContentType ?? "unknown",
                    LastModified = blob.Details?.LastModified ?? DateTime.MinValue
                });
            }

            return Ok(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list files with prefix {Prefix}", prefix);
            return StatusCode(500, "List operation failed");
        }
    }
}

public class FileUploadRequest
{
    public IFormFile File { get; set; } = null!;
    public string? Category { get; set; }
}

public class FileInfo
{
    public string Name { get; set; } = null!;
    public long Size { get; set; }
    public string ContentType { get; set; } = null!;
    public DateTime LastModified { get; set; }
}
```

## Error Handling

```csharp
public class RobustBlobService
{
    private readonly IBlobService _blobService;
    private readonly ILogger<RobustBlobService> _logger;

    public RobustBlobService(IBlobService blobService, ILogger<RobustBlobService> logger)
    {
        _blobService = blobService;
        _logger = logger;
    }

    public async Task<string?> SafeUploadAsync(BlobData blob, int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await _blobService.SaveAsync(blob);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 429) // Too Many Requests
            {
                if (attempt == maxRetries)
                    throw;

                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // Exponential backoff
                _logger.LogWarning("Upload rate limited, retrying in {Delay}s (attempt {Attempt}/{MaxRetries})", 
                    delay.TotalSeconds, attempt, maxRetries);
                
                await Task.Delay(delay);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status >= 500) // Server errors
            {
                if (attempt == maxRetries)
                    throw;

                _logger.LogWarning(ex, "Server error during upload, retrying (attempt {Attempt}/{MaxRetries})", 
                    attempt, maxRetries);
                
                await Task.Delay(TimeSpan.FromSeconds(attempt));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload failed for blob {BlobName}", blob.Name);
                throw;
            }
        }

        return null;
    }
}
```

## Performance Considerations

- **Connection Pooling**: Azure Storage SDK handles connection pooling automatically
- **Parallel Operations**: Use `Task.WhenAll` for concurrent operations
- **Stream Usage**: Always use streams for large files to avoid memory issues
- **Metadata**: Minimize metadata size for better performance
- **Container Access**: Container access is cached within the service

## Security Considerations

- **Connection Strings**: Store connection strings securely (Azure Key Vault, environment variables)
- **SAS Tokens**: Use SAS tokens for limited access scenarios
- **Container Permissions**: Set appropriate container access levels
- **HTTPS**: Always use HTTPS endpoints in production
- **Access Logging**: Enable Azure Storage logging for audit trails

## Thread Safety

- Azure Storage SDK is thread-safe
- Service instance can be used concurrently
- Stream operations should not share streams between threads
- Container client is cached and thread-safe

## Contributing

See the main [CONTRIBUTING.md](../../../../../CONTRIBUTING.md) for guidelines on how to contribute to this project.

## License

This project is licensed under the [MIT License](../../../../../LICENSE).

## Related Packages

- [DKNet.Svc.BlobStorage.Abstractions](../DKNet.Svc.BlobStorage.Abstractions) - Core blob storage abstractions
- [DKNet.Svc.BlobStorage.AwsS3](../DKNet.Svc.BlobStorage.AwsS3) - AWS S3 implementation
- [DKNet.Svc.BlobStorage.Local](../DKNet.Svc.BlobStorage.Local) - Local file system implementation

---

Part of the [DKNet Framework](https://github.com/baoduy/DKNet) - A comprehensive .NET framework for building modern, scalable applications.