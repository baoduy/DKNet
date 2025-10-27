# DKNet.Svc.BlobStorage.Local

[![NuGet](https://img.shields.io/nuget/v/DKNet.Svc.BlobStorage.Local)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.Local/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Svc.BlobStorage.Local)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.Local/)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../../../LICENSE)

Local file system implementation of the DKNet blob storage abstractions, providing file storage capabilities using the
local file system. This package is ideal for development, testing, and scenarios where local storage is preferred over
cloud storage solutions.

## Features

- **Local File System Storage**: Direct integration with the local file system
- **Full IBlobService Implementation**: Complete implementation of DKNet blob storage abstractions
- **Cross-Platform Support**: Works on Windows, Linux, and macOS
- **Directory Management**: Automatic directory creation and management
- **File Metadata**: Support for custom metadata storage via extended attributes or companion files
- **Streaming Support**: Efficient streaming for large file operations
- **Development Friendly**: Perfect for development and testing environments
- **No External Dependencies**: Pure .NET implementation with no external service dependencies

## Supported Frameworks

- .NET 9.0+

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package DKNet.Svc.BlobStorage.Local
```

Or via Package Manager Console:

```powershell
Install-Package DKNet.Svc.BlobStorage.Local
```

## Quick Start

### Configuration Setup

```json
{
  "BlobStorage": {
    "LocalFolder": {
      "RootFolder": "C:\\MyApp\\Files",
      "EnableMetadata": true,
      "MaxFileSize": 104857600,
      "GenerateUniqueNames": false,
      "PathPrefix": "uploads/",
      "AllowedExtensions": [".jpg", ".png", ".pdf", ".docx"],
      "BlockedExtensions": [".exe", ".bat", ".cmd"]
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
    // Add local directory blob service
    services.AddLocalDirectoryBlobService(configuration);
    
    // Or configure manually
    services.Configure<LocalDirectoryOptions>(options =>
    {
        options.RootFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyApp", "Files");
        options.EnableMetadata = true;
        options.MaxFileSize = 100 * 1024 * 1024; // 100MB
        options.AllowedExtensions = new[] { ".jpg", ".png", ".pdf", ".docx" };
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

    public async Task<string> SaveDocumentAsync(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        
        var blob = new BlobData
        {
            Name = $"documents/{DateTime.UtcNow:yyyy/MM/dd}/{file.FileName}",
            ContentStream = stream,
            ContentType = file.ContentType,
            Metadata = new Dictionary<string, string>
            {
                ["original-filename"] = file.FileName,
                ["uploaded-at"] = DateTime.UtcNow.ToString("O"),
                ["file-size"] = file.Length.ToString(),
                ["uploaded-by"] = "user123"
            }
        };

        return await _blobService.SaveAsync(blob);
    }

    public async Task<FileInfo?> GetDocumentInfoAsync(string fileName)
    {
        var request = new BlobRequest($"documents/{fileName}");
        var result = await _blobService.GetItemAsync(request);

        if (result?.Details == null)
            return null;

        return new FileInfo
        {
            Name = result.Name,
            Size = result.Details.ContentLength,
            ContentType = result.Details.ContentType,
            LastModified = result.Details.LastModified,
            CreatedOn = result.Details.CreatedOn
        };
    }
}
```

## Configuration

### Local Directory Options

```csharp
public class LocalDirectoryOptions : BlobServiceOptions
{
    public string? RootFolder { get; set; }
    
    // Inherited from BlobServiceOptions
    public bool EnableMetadata { get; set; } = true;
    public long MaxFileSize { get; set; } = 50 * 1024 * 1024; // 50MB
    public string[] AllowedExtensions { get; set; } = Array.Empty<string>();
    public string[] BlockedExtensions { get; set; } = { ".exe", ".bat", ".cmd" };
    public bool GenerateUniqueNames { get; set; } = false;
    public string PathPrefix { get; set; } = string.Empty;
}
```

### Environment-Specific Configuration

```csharp
// Development configuration
public void ConfigureDevelopmentServices(IServiceCollection services, IConfiguration configuration)
{
    services.Configure<LocalDirectoryOptions>(options =>
    {
        options.RootFolder = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "Files");
        options.EnableMetadata = true;
        options.MaxFileSize = 10 * 1024 * 1024; // 10MB for development
        options.GenerateUniqueNames = true; // Avoid conflicts during development
    });
    
    services.AddLocalDirectoryBlobService(configuration);
}

// Production configuration
public void ConfigureProductionServices(IServiceCollection services, IConfiguration configuration)
{
    services.Configure<LocalDirectoryOptions>(options =>
    {
        options.RootFolder = configuration["Storage:LocalPath"] ?? "/var/app/files";
        options.EnableMetadata = true;
        options.MaxFileSize = 500 * 1024 * 1024; // 500MB for production
        options.AllowedExtensions = new[] { ".pdf", ".jpg", ".png", ".docx", ".xlsx" };
        options.BlockedExtensions = new[] { ".exe", ".bat", ".cmd", ".ps1", ".sh" };
    });
    
    services.AddLocalDirectoryBlobService(configuration);
}

// Docker configuration
public void ConfigureDockerServices(IServiceCollection services, IConfiguration configuration)
{
    services.Configure<LocalDirectoryOptions>(options =>
    {
        options.RootFolder = "/app/data/files"; // Docker volume mount
        options.EnableMetadata = true;
        options.MaxFileSize = 100 * 1024 * 1024;
    });
    
    services.AddLocalDirectoryBlobService(configuration);
}
```

## API Reference

### LocalBlobService

Implements `IBlobService` with local file system backend:

- `SaveAsync(BlobData, CancellationToken)` - Save file to local directory
- `GetAsync(BlobRequest, CancellationToken)` - Read file from local directory
- `GetItemAsync(BlobRequest, CancellationToken)` - Get file metadata only
- `ListItemsAsync(BlobRequest, CancellationToken)` - List files in directory
- `DeleteAsync(BlobRequest, CancellationToken)` - Delete file from local directory
- `ExistsAsync(BlobRequest, CancellationToken)` - Check if file exists

### LocalDirectoryOptions

Configuration class extending `BlobServiceOptions`:

- `RootFolder` - Base directory for file storage
- Plus all base blob service options

### Setup Extensions

- `AddLocalDirectoryBlobService(IConfiguration)` - Register local directory implementation
- `IsDirectory(string)` - Utility method to check if path is a directory

## Advanced Usage

### Custom File Organization

```csharp
public class OrganizedFileService
{
    private readonly IBlobService _blobService;

    public OrganizedFileService(IBlobService blobService)
    {
        _blobService = blobService;
    }

    public async Task<string> SaveFileByTypeAsync(IFormFile file, string category, string userId)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileType = GetFileType(extension);
        var fileName = GenerateFileName(file.FileName, userId);
        
        var blob = new BlobData
        {
            Name = $"{category}/{fileType}/{DateTime.UtcNow:yyyy/MM}/{fileName}",
            ContentStream = file.OpenReadStream(),
            ContentType = file.ContentType,
            Metadata = new Dictionary<string, string>
            {
                ["category"] = category,
                ["file-type"] = fileType,
                ["user-id"] = userId,
                ["original-name"] = file.FileName,
                ["upload-date"] = DateTime.UtcNow.ToString("O")
            }
        };

        return await _blobService.SaveAsync(blob);
    }

    private static string GetFileType(string extension) => extension switch
    {
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "images",
        ".pdf" => "documents",
        ".docx" or ".doc" or ".xlsx" or ".xls" or ".pptx" or ".ppt" => "office",
        ".mp4" or ".avi" or ".mov" or ".wmv" => "videos",
        ".mp3" or ".wav" or ".flac" or ".aac" => "audio",
        _ => "misc"
    };

    private static string GenerateFileName(string originalName, string userId)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(originalName);
        var extension = Path.GetExtension(originalName);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        return $"{nameWithoutExtension}_{userId}_{timestamp}{extension}";
    }
}
```

### File Archive and Cleanup

```csharp
public class FileArchiveService
{
    private readonly IBlobService _blobService;
    private readonly LocalDirectoryOptions _options;
    private readonly ILogger<FileArchiveService> _logger;

    public FileArchiveService(
        IBlobService blobService, 
        IOptions<LocalDirectoryOptions> options,
        ILogger<FileArchiveService> logger)
    {
        _blobService = blobService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ArchiveOldFilesAsync(TimeSpan maxAge, string archivePrefix = "archive")
    {
        var cutoffDate = DateTime.UtcNow - maxAge;
        var request = new BlobRequest("") { Type = BlobTypes.Directory };
        var archivedCount = 0;

        await foreach (var file in _blobService.ListItemsAsync(request))
        {
            if (file.Details?.LastModified < cutoffDate && !file.Name.StartsWith(archivePrefix))
            {
                try
                {
                    // Read original file
                    var originalRequest = new BlobRequest(file.Name);
                    var originalData = await _blobService.GetAsync(originalRequest);
                    
                    if (originalData != null)
                    {
                        // Create archived version
                        var archiveBlob = new BlobData
                        {
                            Name = $"{archivePrefix}/{DateTime.UtcNow:yyyy/MM}/{file.Name}",
                            ContentStream = originalData.ContentStream,
                            ContentType = originalData.ContentType,
                            Metadata = new Dictionary<string, string>(originalData.Metadata ?? new Dictionary<string, string>())
                            {
                                ["archived-date"] = DateTime.UtcNow.ToString("O"),
                                ["original-path"] = file.Name
                            }
                        };

                        await _blobService.SaveAsync(archiveBlob);
                        await _blobService.DeleteAsync(originalRequest);
                        
                        archivedCount++;
                        _logger.LogInformation("Archived file: {FileName}", file.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to archive file: {FileName}", file.Name);
                }
            }
        }

        _logger.LogInformation("Archived {Count} files older than {MaxAge}", archivedCount, maxAge);
    }

    public async Task<long> CalculateDirectorySizeAsync(string? prefix = null)
    {
        var request = new BlobRequest(prefix ?? "") { Type = BlobTypes.Directory };
        long totalSize = 0;

        await foreach (var file in _blobService.ListItemsAsync(request))
        {
            totalSize += file.Details?.ContentLength ?? 0;
        }

        return totalSize;
    }

    public async Task CleanupEmptyDirectoriesAsync()
    {
        if (string.IsNullOrEmpty(_options.RootFolder))
            return;

        await CleanupEmptyDirectoriesRecursive(_options.RootFolder);
    }

    private async Task CleanupEmptyDirectoriesRecursive(string directoryPath)
    {
        try
        {
            var subdirectories = Directory.GetDirectories(directoryPath);
            
            foreach (var subdirectory in subdirectories)
            {
                await CleanupEmptyDirectoriesRecursive(subdirectory);
            }

            // Check if directory is empty after cleaning subdirectories
            if (!Directory.EnumerateFileSystemEntries(directoryPath).Any())
            {
                Directory.Delete(directoryPath);
                _logger.LogDebug("Removed empty directory: {DirectoryPath}", directoryPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup directory: {DirectoryPath}", directoryPath);
        }
    }
}
```

### File Synchronization and Backup

```csharp
public class FileSyncService
{
    private readonly IBlobService _blobService;
    private readonly ILogger<FileSyncService> _logger;

    public FileSyncService(IBlobService blobService, ILogger<FileSyncService> logger)
    {
        _blobService = blobService;
        _logger = logger;
    }

    public async Task SyncToBackupDirectoryAsync(string backupPath)
    {
        Directory.CreateDirectory(backupPath);
        
        var request = new BlobRequest("") { Type = BlobTypes.Directory };
        var syncedCount = 0;

        await foreach (var file in _blobService.ListItemsAsync(request))
        {
            try
            {
                var backupFilePath = Path.Combine(backupPath, file.Name);
                var backupDirectory = Path.GetDirectoryName(backupFilePath);
                
                if (!string.IsNullOrEmpty(backupDirectory))
                {
                    Directory.CreateDirectory(backupDirectory);
                }

                // Check if backup is needed (file doesn't exist or is older)
                if (!File.Exists(backupFilePath) || 
                    File.GetLastWriteTime(backupFilePath) < file.Details?.LastModified)
                {
                    var fileData = await _blobService.GetAsync(new BlobRequest(file.Name));
                    if (fileData?.ContentStream != null)
                    {
                        using var outputStream = File.Create(backupFilePath);
                        await fileData.ContentStream.CopyToAsync(outputStream);
                        
                        // Preserve timestamps
                        if (file.Details?.LastModified != null)
                        {
                            File.SetLastWriteTime(backupFilePath, file.Details.LastModified);
                        }
                        
                        syncedCount++;
                        _logger.LogDebug("Synced file: {FileName}", file.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync file: {FileName}", file.Name);
            }
        }

        _logger.LogInformation("Synced {Count} files to backup directory", syncedCount);
    }

    public async Task<List<string>> FindDuplicateFilesAsync()
    {
        var request = new BlobRequest("") { Type = BlobTypes.Directory };
        var fileHashes = new Dictionary<string, List<string>>();
        var duplicates = new List<string>();

        await foreach (var file in _blobService.ListItemsAsync(request))
        {
            try
            {
                var fileData = await _blobService.GetAsync(new BlobRequest(file.Name));
                if (fileData?.Content != null)
                {
                    var hash = ComputeHash(fileData.Content);
                    
                    if (!fileHashes.ContainsKey(hash))
                    {
                        fileHashes[hash] = new List<string>();
                    }
                    
                    fileHashes[hash].Add(file.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to hash file: {FileName}", file.Name);
            }
        }

        foreach (var hashGroup in fileHashes.Where(kvp => kvp.Value.Count > 1))
        {
            _logger.LogInformation("Found {Count} duplicate files with hash {Hash}: {Files}", 
                hashGroup.Value.Count, hashGroup.Key, string.Join(", ", hashGroup.Value));
            
            duplicates.AddRange(hashGroup.Value.Skip(1)); // Keep first, mark others as duplicates
        }

        return duplicates;
    }

    private static string ComputeHash(byte[] data)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(data);
        return Convert.ToBase64String(hashBytes);
    }
}
```

### Integration with File Watchers

```csharp
public class FileWatcherService : IHostedService, IDisposable
{
    private readonly LocalDirectoryOptions _options;
    private readonly ILogger<FileWatcherService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private FileSystemWatcher? _watcher;

    public FileWatcherService(
        IOptions<LocalDirectoryOptions> options,
        ILogger<FileWatcherService> logger,
        IServiceProvider serviceProvider)
    {
        _options = options.Value;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.RootFolder))
            return Task.CompletedTask;

        _watcher = new FileSystemWatcher(_options.RootFolder)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnFileCreated;
        _watcher.Changed += OnFileChanged;
        _watcher.Deleted += OnFileDeleted;
        _watcher.Renamed += OnFileRenamed;

        _logger.LogInformation("File watcher started for directory: {Directory}", _options.RootFolder);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        _logger.LogInformation("File watcher stopped");
        return Task.CompletedTask;
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("File created: {FilePath}", e.FullPath);
        _ = Task.Run(() => ProcessFileEvent("Created", e.FullPath));
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("File changed: {FilePath}", e.FullPath);
        _ = Task.Run(() => ProcessFileEvent("Changed", e.FullPath));
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("File deleted: {FilePath}", e.FullPath);
        _ = Task.Run(() => ProcessFileEvent("Deleted", e.FullPath));
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        _logger.LogDebug("File renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);
        _ = Task.Run(() => ProcessFileEvent("Renamed", e.FullPath, e.OldFullPath));
    }

    private async Task ProcessFileEvent(string eventType, string filePath, string? oldPath = null)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var eventProcessor = scope.ServiceProvider.GetService<IFileEventProcessor>();
            
            if (eventProcessor != null)
            {
                await eventProcessor.ProcessAsync(eventType, filePath, oldPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file event {EventType} for {FilePath}", eventType, filePath);
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}

public interface IFileEventProcessor
{
    Task ProcessAsync(string eventType, string filePath, string? oldPath = null);
}
```

## Performance Considerations

- **File System Performance**: Performance depends on underlying file system (NTFS, ext4, etc.)
- **Directory Structure**: Avoid too many files in a single directory (consider date-based organization)
- **Concurrent Access**: File system handles concurrent reads but writes may need coordination
- **Large Files**: Use streaming for large files to avoid memory issues
- **Metadata Storage**: Metadata is stored in companion files (.metadata) or extended attributes

## Security Considerations

- **File Permissions**: Ensure appropriate file system permissions for the service account
- **Path Validation**: Validate file paths to prevent directory traversal attacks
- **File Extensions**: Use allowed/blocked extension lists to prevent execution of dangerous files
- **Virus Scanning**: Consider integrating with antivirus solutions for uploaded files
- **Access Logging**: Log file access for audit trails

## Platform Considerations

### Windows

- Supports extended attributes for metadata
- File paths limited to 260 characters (unless long path support enabled)
- Case-insensitive file names

### Linux/macOS

- Supports extended attributes (xattr) for metadata
- Case-sensitive file names
- Better support for long file paths
- Consider file permissions and ownership

## Thread Safety

- File system operations are thread-safe at the OS level
- Service instance can be used concurrently
- Consider file locking for critical operations
- Stream operations should not share streams between threads

## Contributing

See the main [CONTRIBUTING.md](../../../../../CONTRIBUTING.md) for guidelines on how to contribute to this project.

## License

This project is licensed under the [MIT License](../../../../../LICENSE).

## Related Packages

- [DKNet.Svc.BlobStorage.Abstractions](../DKNet.Svc.BlobStorage.Abstractions) - Core blob storage abstractions
- [DKNet.Svc.BlobStorage.AzureStorage](../DKNet.Svc.BlobStorage.AzureStorage) - Azure Blob Storage implementation
- [DKNet.Svc.BlobStorage.AwsS3](../DKNet.Svc.BlobStorage.AwsS3) - AWS S3 implementation

---

Part of the [DKNet Framework](https://github.com/baoduy/DKNet) - A comprehensive .NET framework for building modern,
scalable applications.