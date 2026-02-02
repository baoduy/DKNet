# DKNet.Svc.BlobStorage.Abstractions

[![NuGet](https://img.shields.io/nuget/v/DKNet.Svc.BlobStorage.Abstractions)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.Abstractions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Svc.BlobStorage.Abstractions)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.Abstractions/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../../../LICENSE)

Core abstractions and interfaces for blob storage services, providing a unified API for file storage operations across
different providers (Azure Blob Storage, AWS S3, Local File System). This package defines contracts for storage
operations, data models, and configuration options.

## Features

- **Provider-Agnostic Interface**: Unified blob storage API works with any storage provider
- **Async/Await Support**: Full async operations with cancellation token support
- **Rich Metadata Support**: Comprehensive blob metadata including content type, size, timestamps
- **Stream Operations**: Efficient stream-based operations for large files
- **Directory Operations**: Support for directory-like operations and listing
- **Extension Methods**: Convenient extension methods for common operations
- **Configuration Options**: Flexible configuration for different storage scenarios
- **Type Safety**: Strongly-typed models for requests, responses, and metadata

## Supported Frameworks

- .NET 10.0+

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package DKNet.Svc.BlobStorage.Abstractions
```

Or via Package Manager Console:

```powershell
Install-Package DKNet.Svc.BlobStorage.Abstractions
```

## Quick Start

### Basic Interface Usage

```csharp
using DKNet.Svc.BlobStorage.Abstractions;

public class DocumentService
{
    private readonly IBlobService _blobService;

    public DocumentService(IBlobService blobService)
    {
        _blobService = blobService;
    }

    public async Task<string> UploadDocumentAsync(string fileName, byte[] content, string contentType)
    {
        var blob = new BlobData
        {
            Name = $"documents/{fileName}",
            Content = content,
            ContentType = contentType
        };

        return await _blobService.SaveAsync(blob);
    }

    public async Task<byte[]?> DownloadDocumentAsync(string fileName)
    {
        var request = new BlobRequest($"documents/{fileName}");
        var result = await _blobService.GetAsync(request);
        
        return result?.Content;
    }
}
```

### Working with Streams

```csharp
public async Task<string> UploadLargeFileAsync(string fileName, Stream fileStream, string contentType)
{
    var blob = new BlobData
    {
        Name = $"uploads/{fileName}",
        ContentStream = fileStream,
        ContentType = contentType
    };

    return await _blobService.SaveAsync(blob);
}

public async Task<Stream?> DownloadAsStreamAsync(string fileName)
{
    var request = new BlobRequest($"uploads/{fileName}");
    var result = await _blobService.GetAsync(request);
    
    return result?.ContentStream;
}
```

## Configuration

### Service Registration Pattern

```csharp
using Microsoft.Extensions.DependencyInjection;
using DKNet.Svc.BlobStorage.Abstractions;

// Register your chosen implementation
services.Configure<BlobServiceOptions>(options =>
{
    options.ContainerName = "my-container";
    options.EnableMetadata = true;
    options.MaxFileSize = 100 * 1024 * 1024; // 100MB
});

// IBlobService implementation will be provided by specific provider package
// e.g., DKNet.Svc.BlobStorage.AzureStorage, DKNet.Svc.BlobStorage.AwsS3
```

### Blob Service Options

```csharp
public class BlobServiceOptions
{
    public string ContainerName { get; set; } = "default";
    public bool EnableMetadata { get; set; } = true;
    public long MaxFileSize { get; set; } = 50 * 1024 * 1024; // 50MB default
    public string[] AllowedExtensions { get; set; } = Array.Empty<string>();
    public string[] BlockedExtensions { get; set; } = { ".exe", ".bat", ".cmd" };
    public bool GenerateUniqueNames { get; set; } = false;
    public string PathPrefix { get; set; } = string.Empty;
}
```

## API Reference

### Core Interface

- `IBlobService` - Main blob storage operations interface

### Primary Methods

- `SaveAsync(BlobData, CancellationToken)` - Save blob data and return location
- `GetAsync(BlobRequest, CancellationToken)` - Retrieve blob with content
- `GetItemAsync(BlobRequest, CancellationToken)` - Retrieve blob metadata only
- `ListItemsAsync(BlobRequest, CancellationToken)` - List blobs with optional filtering
- `DeleteAsync(BlobRequest, CancellationToken)` - Delete blob
- `ExistsAsync(BlobRequest, CancellationToken)` - Check if blob exists

### Data Models

- `BlobData` - Blob content with metadata for upload operations
- `BlobRequest` - Request parameters for blob operations
- `BlobResult` - Blob metadata without content
- `BlobDataResult` - Blob metadata with content
- `BlobDetails` - Detailed blob information (size, type, timestamps)
- `BlobTypes` - Enumeration for File/Directory types

### Extension Methods

- `SaveTextAsync(string, string)` - Save text content directly
- `GetTextAsync(string)` - Retrieve content as text
- `SaveJsonAsync<T>(string, T)` - Save object as JSON
- `GetJsonAsync<T>(string)` - Retrieve and deserialize JSON

## Advanced Usage

### Working with Metadata

```csharp
public async Task<BlobDetails?> GetFileInfoAsync(string fileName)
{
    var request = new BlobRequest(fileName);
    var result = await _blobService.GetItemAsync(request);
    
    return result?.Details;
}

public async Task ListDocumentsAsync()
{
    var request = new BlobRequest("documents/")
    {
        Type = BlobTypes.Directory
    };

    await foreach (var blob in _blobService.ListItemsAsync(request))
    {
        Console.WriteLine($"File: {blob.Name}");
        if (blob.Details != null)
        {
            Console.WriteLine($"  Size: {blob.Details.ContentLength} bytes");
            Console.WriteLine($"  Type: {blob.Details.ContentType}");
            Console.WriteLine($"  Modified: {blob.Details.LastModified}");
        }
    }
}
```

### Custom Blob Operations

```csharp
public class ImageService
{
    private readonly IBlobService _blobService;
    private readonly IImageProcessor _imageProcessor;

    public ImageService(IBlobService blobService, IImageProcessor imageProcessor)
    {
        _blobService = blobService;
        _imageProcessor = imageProcessor;
    }

    public async Task<string> UploadImageWithThumbnailAsync(string fileName, byte[] imageData)
    {
        // Upload original image
        var originalBlob = new BlobData
        {
            Name = $"images/original/{fileName}",
            Content = imageData,
            ContentType = "image/jpeg"
        };
        
        var originalLocation = await _blobService.SaveAsync(originalBlob);

        // Create and upload thumbnail
        var thumbnailData = await _imageProcessor.CreateThumbnailAsync(imageData, 150, 150);
        var thumbnailBlob = new BlobData
        {
            Name = $"images/thumbnails/{fileName}",
            Content = thumbnailData,
            ContentType = "image/jpeg"
        };
        
        await _blobService.SaveAsync(thumbnailBlob);

        return originalLocation;
    }
}
```

### Batch Operations

```csharp
public async Task<List<string>> UploadMultipleFilesAsync(IEnumerable<(string name, byte[] content, string contentType)> files)
{
    var uploadTasks = files.Select(async file =>
    {
        var blob = new BlobData
        {
            Name = file.name,
            Content = file.content,
            ContentType = file.contentType
        };
        
        return await _blobService.SaveAsync(blob);
    });

    return (await Task.WhenAll(uploadTasks)).ToList();
}

public async Task DeleteMultipleFilesAsync(IEnumerable<string> fileNames)
{
    var deleteTasks = fileNames.Select(async fileName =>
    {
        var request = new BlobRequest(fileName);
        await _blobService.DeleteAsync(request);
    });

    await Task.WhenAll(deleteTasks);
}
```

### Integration with ASP.NET Core

```csharp
[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly IBlobService _blobService;

    public FilesController(IBlobService blobService)
    {
        _blobService = blobService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        using var stream = file.OpenReadStream();
        var blob = new BlobData
        {
            Name = $"uploads/{Guid.NewGuid()}/{file.FileName}",
            ContentStream = stream,
            ContentType = file.ContentType
        };

        var location = await _blobService.SaveAsync(blob);
        return Ok(new { Location = location });
    }

    [HttpGet("download/{fileName}")]
    public async Task<IActionResult> DownloadFile(string fileName)
    {
        var request = new BlobRequest($"uploads/{fileName}");
        var result = await _blobService.GetAsync(request);

        if (result == null)
            return NotFound();

        return File(result.Content, result.ContentType ?? "application/octet-stream", fileName);
    }
}
```

## Error Handling

```csharp
public async Task<string?> SafeUploadAsync(string fileName, byte[] content, string contentType)
{
    try
    {
        var blob = new BlobData
        {
            Name = fileName,
            Content = content,
            ContentType = contentType
        };

        return await _blobService.SaveAsync(blob);
    }
    catch (ArgumentException ex)
    {
        // Handle invalid file name or content
        _logger.LogError(ex, "Invalid file data for {FileName}", fileName);
        return null;
    }
    catch (InvalidOperationException ex)
    {
        // Handle storage service errors
        _logger.LogError(ex, "Storage service error for {FileName}", fileName);
        return null;
    }
    catch (Exception ex)
    {
        // Handle unexpected errors
        _logger.LogError(ex, "Unexpected error uploading {FileName}", fileName);
        throw;
    }
}
```

## Performance Considerations

- **Stream Usage**: Use streams for large files to avoid memory issues
- **Async Operations**: All operations are async for non-blocking I/O
- **Batch Operations**: Use parallel operations for multiple files
- **Content Type Detection**: Set appropriate content types for better caching
- **Metadata Caching**: Cache blob metadata when appropriate

## Thread Safety

- Interface implementations should be thread-safe
- Concurrent operations on different blobs are safe
- Stream operations should not share streams between threads

## Contributing

See the main [CONTRIBUTING.md](../../../../../CONTRIBUTING.md) for guidelines on how to contribute to this project.

## License

This project is licensed under the [MIT License](../../../../../LICENSE).

## Related Packages

- [DKNet.Svc.BlobStorage.AzureStorage](../DKNet.Svc.BlobStorage.AzureStorage) - Azure Blob Storage implementation
- [DKNet.Svc.BlobStorage.AwsS3](../DKNet.Svc.BlobStorage.AwsS3) - AWS S3 implementation
- [DKNet.Svc.BlobStorage.Local](../DKNet.Svc.BlobStorage.Local) - Local file system implementation

---

Part of the [DKNet Framework](https://github.com/baoduy/DKNet) - A comprehensive .NET framework for building modern,
scalable applications.