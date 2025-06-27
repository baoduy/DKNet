# DKNet.Svc.BlobStorage.Local

**Local file system storage adapter implementation that provides file storage operations using the local file system, implementing the blob storage abstractions defined in DKNet.Svc.BlobStorage.Abstractions while supporting Domain-Driven Design (DDD) and Onion Architecture principles for development, testing, and on-premises scenarios.**

## What is this project?

DKNet.Svc.BlobStorage.Local provides a complete implementation of the blob storage abstractions for local file system storage, enabling applications to store, retrieve, and manage files on the local disk or network file shares. This adapter is ideal for development environments, testing scenarios, on-premises deployments, and applications that require local file storage without cloud dependencies.

### Key Features

- **LocalBlobService**: Complete IBlobService implementation for local file system
- **LocalDirectoryOptions**: Comprehensive configuration options for local storage
- **LocalDirectorySetup**: Streamlined service registration and configuration
- **File System Integration**: Direct integration with .NET file system APIs
- **Path Management**: Secure path handling and directory traversal protection
- **Metadata Storage**: File metadata storage using extended attributes or sidecar files
- **Performance Optimization**: Efficient file operations with streaming and caching
- **Cross-Platform**: Works on Windows, Linux, and macOS
- **Development Friendly**: Perfect for local development and testing
- **Network Share Support**: Can work with UNC paths and mapped drives

## How it contributes to DDD and Onion Architecture

### Infrastructure Layer Implementation

DKNet.Svc.BlobStorage.Local implements the **Infrastructure Layer** of the Onion Architecture, providing concrete local file system storage capabilities without affecting higher layers:

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 Presentation Layer                        │
│                   (Controllers, API Endpoints)                  │
│                                                                 │
│  Uses: File upload/download endpoints with local URLs          │
│  Returns: Local file paths, download streams, upload results   │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🎯 Application Layer                          │
│              (Use Cases, Application Services)                  │
│                                                                 │
│  Depends on: IBlobService abstraction                          │
│  Benefits from: Fast local access, no cloud dependencies       │
│  Orchestrates: File processing workflows with local storage    │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                             │
│           (Entities, Aggregates, Domain Services)              │
│                                                                 │
│  🎭 Domain entities reference file locations as value objects  │
│  📝 File metadata as business concepts (path, directory)       │
│  🏷️ Completely unaware of local file system implementation    │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🗄️ Infrastructure Layer                        │
│                  (Local File System Implementation)            │
│                                                                 │
│  💾 LocalBlobService - File system operations                 │
│  🔧 LocalDirectoryOptions - Local path configuration          │
│  ⚙️ LocalDirectorySetup - Service registration and setup      │
│  🔒 Path security and validation                              │
│  📊 File system monitoring and diagnostics                    │
│  🌍 Cross-platform file system compatibility                  │
└─────────────────────────────────────────────────────────────────┘
```

### DDD Benefits

1. **Domain Independence**: Domain layer unaware of local file system specifics
2. **Development Efficiency**: Fast local development without cloud setup
3. **Testing Simplicity**: Easy unit and integration testing with local files
4. **Offline Capability**: Applications can work without internet connectivity
5. **Cost Effectiveness**: No cloud storage costs for development and testing
6. **Data Locality**: Fast access for performance-critical applications

### Onion Architecture Benefits

1. **Dependency Inversion**: Infrastructure implements abstractions defined in higher layers
2. **Technology Flexibility**: Easy to switch between local and cloud storage
3. **Testability**: Local adapter perfect for automated testing scenarios
4. **Separation of Concerns**: File system logic isolated from business logic
5. **Configuration Management**: Centralized local storage configuration
6. **Development Productivity**: Rapid iteration without cloud dependencies

## How to use it

### Installation

```bash
dotnet add package DKNet.Svc.BlobStorage.Local
dotnet add package DKNet.Svc.BlobStorage.Abstractions
```

### Basic Usage Examples

#### 1. Configuration and Setup

```csharp
using DKNet.Svc.BlobStorage.Local;
using DKNet.Svc.BlobStorage.Abstractions;

// appsettings.json configuration
{
  "LocalBlobStorage": {
    "RootPath": "C:\\App\\Storage", // Windows
    // "RootPath": "/var/app/storage", // Linux/macOS
    "CreateDirectoryIfNotExists": true,
    "EnableMetadataStorage": true,
    "MetadataStorageType": "SidecarFiles", // Options: SidecarFiles, ExtendedAttributes
    "MaxFileSize": 104857600, // 100MB
    "AllowedFileExtensions": [".jpg", ".jpeg", ".png", ".pdf", ".docx", ".txt"],
    "PreserveDirectoryStructure": true,
    "EnableFileWatcher": true,
    "TempDirectory": "temp",
    "BackupDirectory": "backups"
  }
}

// Service registration
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLocalBlobStorage(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Configure local storage options
        services.Configure<LocalDirectoryOptions>(configuration.GetSection("LocalBlobStorage"));
        
        // Register blob storage service
        services.AddScoped<IBlobService, LocalBlobService>();
        
        // Optional: Register file watcher service
        services.AddSingleton<IFileWatcherService, FileWatcherService>();
        
        return services;
    }
    
    // Alternative with explicit configuration
    public static IServiceCollection AddLocalBlobStorageWithPath(
        this IServiceCollection services,
        string rootPath,
        Action<LocalDirectoryOptions>? configureOptions = null)
    {
        services.Configure<LocalDirectoryOptions>(options =>
        {
            options.RootPath = rootPath;
            options.CreateDirectoryIfNotExists = true;
            options.EnableMetadataStorage = true;
            
            configureOptions?.Invoke(options);
        });
        
        services.AddScoped<IBlobService, LocalBlobService>();
        
        return services;
    }
}

// Program.cs or Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Method 1: Configuration-based setup
    services.AddLocalBlobStorage(Configuration);
    
    // Method 2: Explicit path setup
    services.AddLocalBlobStorageWithPath(
        Path.Combine(Environment.ContentRootPath, "Storage"),
        options =>
        {
            options.EnableFileWatcher = true;
            options.MetadataStorageType = "ExtendedAttributes";
        });
}
```

#### 2. Basic File Operations

```csharp
public class DocumentService
{
    private readonly IBlobService _blobService;
    private readonly ILogger<DocumentService> _logger;
    
    public DocumentService(IBlobService blobService, ILogger<DocumentService> logger)
    {
        _blobService = blobService;
        _logger = logger;
    }
    
    // Upload document to local storage
    public async Task<string> UploadDocumentAsync(IFormFile file, string userId, string category)
    {
        try
        {
            var fileName = GenerateFileName(file.FileName, userId, category);
            
            using var stream = file.OpenReadStream();
            var blobData = new BlobData
            {
                FileName = fileName,
                ContentType = file.ContentType,
                Content = stream,
                Metadata = new Dictionary<string, string>
                {
                    ["UploadedBy"] = userId,
                    ["Category"] = category,
                    ["OriginalFileName"] = file.FileName,
                    ["UploadedAt"] = DateTime.UtcNow.ToString("O"),
                    ["FileSize"] = file.Length.ToString(),
                    ["CheckSum"] = await ComputeChecksumAsync(file.OpenReadStream())
                }
            };
            
            var result = await _blobService.SaveAsync(blobData);
            
            _logger.LogInformation("Document uploaded to local storage: {FileName} -> {LocalPath}", 
                file.FileName, result.FileName);
            
            return result.FileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload document to local storage: {FileName}", file.FileName);
            throw;
        }
    }
    
    // Download document from local storage
    public async Task<FileResult> DownloadDocumentAsync(string fileName)
    {
        try
        {
            var exists = await _blobService.ExistsAsync(fileName);
            if (!exists)
            {
                throw new FileNotFoundException($"Document not found: {fileName}");
            }
            
            var blobData = await _blobService.GetAsync(fileName);
            
            // Verify file integrity
            var storedChecksum = blobData.Metadata.GetValueOrDefault("CheckSum");
            if (!string.IsNullOrEmpty(storedChecksum))
            {
                var currentChecksum = await ComputeChecksumAsync(blobData.Content);
                if (storedChecksum != currentChecksum)
                {
                    _logger.LogWarning("File integrity check failed for: {FileName}", fileName);
                }
            }
            
            return new FileStreamResult(blobData.Content, blobData.ContentType)
            {
                FileDownloadName = GetOriginalFileName(blobData.Metadata) ?? Path.GetFileName(fileName)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download document from local storage: {FileName}", fileName);
            throw;
        }
    }
    
    // Get local file path for direct access
    public async Task<string> GetLocalFilePathAsync(string fileName)
    {
        var exists = await _blobService.ExistsAsync(fileName);
        if (!exists)
        {
            throw new FileNotFoundException($"File not found: {fileName}");
        }
        
        // This is specific to local storage - get actual file path
        if (_blobService is LocalBlobService localService)
        {
            return localService.GetPhysicalPath(fileName);
        }
        
        throw new InvalidOperationException("This operation is only supported for local storage");
    }
    
    // List files in directory
    public async Task<IEnumerable<BlobInfo>> ListUserDocumentsAsync(string userId, string category = null)
    {
        var prefix = string.IsNullOrEmpty(category) 
            ? $"{userId}/" 
            : $"{userId}/{category}/";
            
        return await _blobService.ListAsync(prefix);
    }
    
    // Move file to different category
    public async Task MoveDocumentAsync(string fileName, string newCategory)
    {
        try
        {
            var exists = await _blobService.ExistsAsync(fileName);
            if (!exists)
            {
                throw new FileNotFoundException($"File not found: {fileName}");
            }
            
            // Get file data
            var blobData = await _blobService.GetAsync(fileName);
            
            // Create new file name with different category
            var pathParts = fileName.Split('/');
            if (pathParts.Length >= 3)
            {
                pathParts[1] = newCategory; // Assuming format: userId/category/filename
                var newFileName = string.Join("/", pathParts);
                
                // Save to new location
                blobData.FileName = newFileName;
                await _blobService.SaveAsync(blobData);
                
                // Delete old file
                await _blobService.DeleteAsync(fileName);
                
                _logger.LogInformation("Document moved: {OldPath} -> {NewPath}", fileName, newFileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move document: {FileName}", fileName);
            throw;
        }
    }
    
    private string GenerateFileName(string originalFileName, string userId, string category)
    {
        var extension = Path.GetExtension(originalFileName);
        var safeFileName = Path.GetFileNameWithoutExtension(originalFileName)
            .Replace(" ", "_")
            .Replace("#", "_");
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        
        return $"{userId}/{category}/{timestamp}_{safeFileName}{extension}";
    }
    
    private async Task<string> ComputeChecksumAsync(Stream stream)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToBase64String(hash);
    }
    
    private string GetOriginalFileName(IDictionary<string, string> metadata)
    {
        return metadata.GetValueOrDefault("OriginalFileName");
    }
}
```

#### 3. Advanced Local Storage Features

```csharp
public class AdvancedLocalStorageService
{
    private readonly LocalDirectoryOptions _options;
    private readonly ILogger<AdvancedLocalStorageService> _logger;
    private readonly IFileWatcherService _fileWatcher;
    
    public AdvancedLocalStorageService(
        IOptions<LocalDirectoryOptions> options,
        ILogger<AdvancedLocalStorageService> logger,
        IFileWatcherService fileWatcher)
    {
        _options = options.Value;
        _logger = logger;
        _fileWatcher = fileWatcher;
    }
    
    // Batch file operations
    public async Task<BatchOperationResult> BatchCopyAsync(IEnumerable<string> sourceFiles, string destinationPrefix)
    {
        var results = new List<BatchOperationItem>();
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount); // Limit concurrency
        
        var tasks = sourceFiles.Select(async sourceFile =>
        {
            await semaphore.WaitAsync();
            try
            {
                var destinationFile = $"{destinationPrefix}/{Path.GetFileName(sourceFile)}";
                var fullSourcePath = Path.Combine(_options.RootPath, sourceFile);
                var fullDestinationPath = Path.Combine(_options.RootPath, destinationFile);
                
                // Ensure destination directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(fullDestinationPath));
                
                // Copy file
                await CopyFileAsync(fullSourcePath, fullDestinationPath);
                
                return new BatchOperationItem
                {
                    FileName = sourceFile,
                    Success = true,
                    Message = $"Copied to {destinationFile}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to copy file: {SourceFile}", sourceFile);
                return new BatchOperationItem
                {
                    FileName = sourceFile,
                    Success = false,
                    Message = ex.Message
                };
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        var batchResults = await Task.WhenAll(tasks);
        
        return new BatchOperationResult
        {
            TotalFiles = sourceFiles.Count(),
            SuccessfulOperations = batchResults.Count(r => r.Success),
            FailedOperations = batchResults.Count(r => !r.Success),
            Results = batchResults.ToList()
        };
    }
    
    // File system cleanup operations
    public async Task<CleanupResult> CleanupOldFilesAsync(TimeSpan maxAge, string pathPattern = "*")
    {
        var cleanupResult = new CleanupResult();
        var cutoffDate = DateTime.UtcNow.Subtract(maxAge);
        
        try
        {
            var searchPath = Path.Combine(_options.RootPath, pathPattern);
            var files = Directory.EnumerateFiles(_options.RootPath, pathPattern, SearchOption.AllDirectories);
            
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTime < cutoffDate)
                {
                    try
                    {
                        // Move to backup before deletion if backup is enabled
                        if (!string.IsNullOrEmpty(_options.BackupDirectory))
                        {
                            await BackupFileAsync(file);
                        }
                        
                        File.Delete(file);
                        cleanupResult.DeletedFiles++;
                        cleanupResult.FreedSpace += fileInfo.Length;
                        
                        _logger.LogDebug("Deleted old file: {FilePath}", file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete file: {FilePath}", file);
                        cleanupResult.FailedDeletions++;
                    }
                }
            }
            
            // Clean up empty directories
            await CleanupEmptyDirectoriesAsync(_options.RootPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cleanup operation failed");
            throw;
        }
        
        return cleanupResult;
    }
    
    // File integrity verification
    public async Task<IntegrityCheckResult> VerifyFileIntegrityAsync(string fileName)
    {
        var result = new IntegrityCheckResult { FileName = fileName };
        
        try
        {
            var fullPath = Path.Combine(_options.RootPath, fileName);
            
            if (!File.Exists(fullPath))
            {
                result.IsValid = false;
                result.Issues.Add("File does not exist");
                return result;
            }
            
            // Check file size
            var fileInfo = new FileInfo(fullPath);
            result.FileSize = fileInfo.Length;
            
            // Verify checksum if metadata exists
            var metadataPath = GetMetadataPath(fileName);
            if (File.Exists(metadataPath))
            {
                var metadata = await ReadMetadataAsync(metadataPath);
                if (metadata.TryGetValue("CheckSum", out var storedChecksum))
                {
                    using var fileStream = File.OpenRead(fullPath);
                    var currentChecksum = await ComputeChecksumAsync(fileStream);
                    
                    if (storedChecksum == currentChecksum)
                    {
                        result.ChecksumValid = true;
                    }
                    else
                    {
                        result.IsValid = false;
                        result.Issues.Add("Checksum mismatch");
                    }
                }
            }
            
            // Check file permissions
            try
            {
                using var stream = File.OpenRead(fullPath);
                result.IsReadable = true;
            }
            catch
            {
                result.IsValid = false;
                result.Issues.Add("File is not readable");
            }
            
            result.IsValid = result.Issues.Count == 0;
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Issues.Add($"Verification failed: {ex.Message}");
        }
        
        return result;
    }
    
    // Setup file system monitoring
    public void StartFileSystemMonitoring()
    {
        if (!_options.EnableFileWatcher)
            return;
        
        _fileWatcher.FileCreated += OnFileCreated;
        _fileWatcher.FileModified += OnFileModified;
        _fileWatcher.FileDeleted += OnFileDeleted;
        
        _fileWatcher.StartWatching(_options.RootPath);
        
        _logger.LogInformation("File system monitoring started for: {RootPath}", _options.RootPath);
    }
    
    private async void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("File created: {FilePath}", e.FullPath);
        
        // Optionally trigger business events
        // await _eventPublisher.PublishAsync(new FileCreatedEvent(e.FullPath));
    }
    
    private async void OnFileModified(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("File modified: {FilePath}", e.FullPath);
        
        // Update metadata if needed
        await UpdateFileMetadataAsync(e.FullPath);
    }
    
    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("File deleted: {FilePath}", e.FullPath);
    }
    
    private async Task CopyFileAsync(string source, string destination)
    {
        using var sourceStream = File.OpenRead(source);
        using var destinationStream = File.Create(destination);
        await sourceStream.CopyToAsync(destinationStream);
    }
    
    private async Task BackupFileAsync(string filePath)
    {
        if (string.IsNullOrEmpty(_options.BackupDirectory))
            return;
        
        var backupDir = Path.Combine(_options.RootPath, _options.BackupDirectory);
        Directory.CreateDirectory(backupDir);
        
        var fileName = Path.GetFileName(filePath);
        var backupPath = Path.Combine(backupDir, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{fileName}");
        
        await CopyFileAsync(filePath, backupPath);
    }
    
    private async Task CleanupEmptyDirectoriesAsync(string path)
    {
        foreach (var directory in Directory.GetDirectories(path))
        {
            await CleanupEmptyDirectoriesAsync(directory);
            
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                try
                {
                    Directory.Delete(directory);
                    _logger.LogDebug("Deleted empty directory: {DirectoryPath}", directory);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete empty directory: {DirectoryPath}", directory);
                }
            }
        }
    }
    
    private string GetMetadataPath(string fileName)
    {
        return Path.Combine(_options.RootPath, $"{fileName}.metadata");
    }
    
    private async Task<Dictionary<string, string>> ReadMetadataAsync(string metadataPath)
    {
        var json = await File.ReadAllTextAsync(metadataPath);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    }
    
    private async Task UpdateFileMetadataAsync(string filePath)
    {
        // Update LastModified timestamp in metadata
        var relativePath = Path.GetRelativePath(_options.RootPath, filePath);
        var metadataPath = GetMetadataPath(relativePath);
        
        if (File.Exists(metadataPath))
        {
            var metadata = await ReadMetadataAsync(metadataPath);
            metadata["LastModified"] = DateTime.UtcNow.ToString("O");
            
            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(metadataPath, json);
        }
    }
    
    private async Task<string> ComputeChecksumAsync(Stream stream)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToBase64String(hash);
    }
}

public class BatchOperationResult
{
    public int TotalFiles { get; set; }
    public int SuccessfulOperations { get; set; }
    public int FailedOperations { get; set; }
    public List<BatchOperationItem> Results { get; set; }
}

public class BatchOperationItem
{
    public string FileName { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; }
}

public class CleanupResult
{
    public int DeletedFiles { get; set; }
    public int FailedDeletions { get; set; }
    public long FreedSpace { get; set; }
    
    public string FormattedFreedSpace => FormatBytes(FreedSpace);
    
    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

public class IntegrityCheckResult
{
    public string FileName { get; set; }
    public bool IsValid { get; set; }
    public bool IsReadable { get; set; }
    public bool ChecksumValid { get; set; }
    public long FileSize { get; set; }
    public List<string> Issues { get; set; } = new();
}
```

#### 4. Development and Testing Utilities

```csharp
public class LocalStorageTestUtilities
{
    private readonly IBlobService _blobService;
    private readonly LocalDirectoryOptions _options;
    
    public LocalStorageTestUtilities(IBlobService blobService, IOptions<LocalDirectoryOptions> options)
    {
        _blobService = blobService;
        _options = options.Value;
    }
    
    // Create test data for development/testing
    public async Task SeedTestDataAsync()
    {
        var testFiles = new[]
        {
            new { Name = "test1/documents/sample.pdf", Content = "Sample PDF content", Type = "application/pdf" },
            new { Name = "test1/images/photo.jpg", Content = "Sample JPEG content", Type = "image/jpeg" },
            new { Name = "test2/documents/report.docx", Content = "Sample DOCX content", Type = "application/vnd.openxmlformats-officedocument.wordprocessingml.document" }
        };
        
        foreach (var testFile in testFiles)
        {
            var content = Encoding.UTF8.GetBytes(testFile.Content);
            using var stream = new MemoryStream(content);
            
            var blobData = new BlobData
            {
                FileName = testFile.Name,
                ContentType = testFile.Type,
                Content = stream,
                Metadata = new Dictionary<string, string>
                {
                    ["CreatedBy"] = "TestDataSeeder",
                    ["CreatedAt"] = DateTime.UtcNow.ToString("O"),
                    ["IsTestData"] = "true"
                }
            };
            
            await _blobService.SaveAsync(blobData);
        }
    }
    
    // Clean up test data
    public async Task CleanupTestDataAsync()
    {
        var testFiles = await _blobService.ListAsync("");
        
        foreach (var file in testFiles)
        {
            if (file.Metadata?.GetValueOrDefault("IsTestData") == "true")
            {
                await _blobService.DeleteAsync(file.FileName);
            }
        }
    }
    
    // Create temporary directory for testing
    public string CreateTempDirectory()
    {
        var tempPath = Path.Combine(_options.RootPath, "temp", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);
        return tempPath;
    }
    
    // Verify storage setup
    public async Task<StorageHealthCheck> VerifyStorageHealthAsync()
    {
        var health = new StorageHealthCheck();
        
        try
        {
            // Check if root directory exists and is writable
            if (!Directory.Exists(_options.RootPath))
            {
                health.Issues.Add($"Root directory does not exist: {_options.RootPath}");
            }
            else
            {
                // Test write access
                var testFile = Path.Combine(_options.RootPath, $"test_{Guid.NewGuid()}.tmp");
                try
                {
                    await File.WriteAllTextAsync(testFile, "test");
                    File.Delete(testFile);
                    health.IsWritable = true;
                }
                catch (Exception ex)
                {
                    health.Issues.Add($"No write access: {ex.Message}");
                }
                
                // Check available space
                var drive = new DriveInfo(Path.GetPathRoot(_options.RootPath));
                health.AvailableSpace = drive.AvailableFreeSpace;
                health.TotalSpace = drive.TotalSize;
                
                if (health.AvailableSpace < 100 * 1024 * 1024) // Less than 100MB
                {
                    health.Issues.Add("Low disk space available");
                }
            }
            
            health.IsHealthy = health.Issues.Count == 0;
        }
        catch (Exception ex)
        {
            health.Issues.Add($"Health check failed: {ex.Message}");
        }
        
        return health;
    }
}

public class StorageHealthCheck
{
    public bool IsHealthy { get; set; }
    public bool IsWritable { get; set; }
    public long AvailableSpace { get; set; }
    public long TotalSpace { get; set; }
    public List<string> Issues { get; set; } = new();
    
    public string FormattedAvailableSpace => FormatBytes(AvailableSpace);
    public string FormattedTotalSpace => FormatBytes(TotalSpace);
    
    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
```

#### 5. Cross-Platform Compatibility

```csharp
public static class CrossPlatformPathHelper
{
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;
        
        // Convert to platform-specific path separators
        path = path.Replace('\\', Path.DirectorySeparatorChar)
                  .Replace('/', Path.DirectorySeparatorChar);
        
        // Handle UNC paths on Windows
        if (OperatingSystem.IsWindows() && path.StartsWith($"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}"))
        {
            return path; // UNC path, leave as-is
        }
        
        return Path.GetFullPath(path);
    }
    
    public static bool IsValidFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;
        
        // Check for invalid characters
        var invalidChars = Path.GetInvalidFileNameChars();
        if (fileName.Any(c => invalidChars.Contains(c)))
            return false;
        
        // Check for reserved names on Windows
        if (OperatingSystem.IsWindows())
        {
            var reservedNames = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();
            if (reservedNames.Contains(nameWithoutExtension))
                return false;
        }
        
        return true;
    }
    
    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "file";
        
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
        
        // Ensure it's not empty after sanitization
        if (string.IsNullOrWhiteSpace(sanitized))
            return "file";
        
        return sanitized;
    }
}
```

### Advanced Usage Examples

#### 1. Network Share Integration

```csharp
public class NetworkShareStorageService
{
    private readonly LocalDirectoryOptions _options;
    private readonly ILogger<NetworkShareStorageService> _logger;
    
    public async Task<bool> TestNetworkShareAccessAsync()
    {
        try
        {
            if (!_options.RootPath.StartsWith(@"\\"))
                return true; // Not a network share
            
            // Test network connectivity
            var testFile = Path.Combine(_options.RootPath, $"connectivity_test_{Guid.NewGuid()}.tmp");
            await File.WriteAllTextAsync(testFile, "test");
            File.Delete(testFile);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Network share access test failed: {RootPath}", _options.RootPath);
            return false;
        }
    }
}
```

#### 2. Docker Volume Integration

```csharp
public class DockerVolumeService
{
    public static void ConfigureForDocker(LocalDirectoryOptions options)
    {
        // Configure for Docker volume mounting
        options.RootPath = "/app/storage";
        options.CreateDirectoryIfNotExists = true;
        options.EnableFileWatcher = false; // File watchers don't work well in containers
    }
}
```

## Best Practices

### 1. Security Configuration

```csharp
// Good: Secure path handling
public static class SecurePathHelper
{
    public static string ValidatePath(string basePath, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath));
        
        if (!fullPath.StartsWith(basePath))
        {
            throw new SecurityException("Path traversal attempt detected");
        }
        
        return fullPath;
    }
}

// Good: File extension validation
services.Configure<LocalDirectoryOptions>(options =>
{
    options.AllowedFileExtensions = new[] { ".jpg", ".png", ".pdf", ".docx" };
    options.MaxFileSize = 10 * 1024 * 1024; // 10MB
});
```

### 2. Performance Optimization

```csharp
// Good: Use streaming for large files
public async Task ProcessLargeFileAsync(Stream fileStream, string fileName)
{
    const int bufferSize = 64 * 1024; // 64KB buffer
    
    using var fileWriteStream = File.Create(fileName);
    await fileStream.CopyToAsync(fileWriteStream, bufferSize);
}

// Good: Implement caching for metadata
private readonly MemoryCache _metadataCache = new MemoryCache(new MemoryCacheOptions
{
    SizeLimit = 1000
});
```

### 3. Error Handling

```csharp
// Good: Handle file system exceptions
public async Task<BlobData> SafeGetFileAsync(string fileName)
{
    try
    {
        return await _blobService.GetAsync(fileName);
    }
    catch (FileNotFoundException)
    {
        throw new BlobNotFoundException($"File not found: {fileName}");
    }
    catch (UnauthorizedAccessException)
    {
        throw new BlobAccessDeniedException($"Access denied: {fileName}");
    }
    catch (IOException ex)
    {
        throw new BlobStorageException($"I/O error accessing file: {fileName}", ex);
    }
}
```

## Integration with Other DKNet Components

DKNet.Svc.BlobStorage.Local integrates seamlessly with other DKNet components:

- **DKNet.Svc.BlobStorage.Abstractions**: Implements the core blob storage contracts
- **DKNet.EfCore.Events**: Supports file-related domain events (FileUploaded, FileDeleted)
- **DKNet.EfCore.Hooks**: Enables file operation hooks and auditing
- **DKNet.SlimBus.Extensions**: Integrates with CQRS for file processing workflows
- **DKNet.Fw.Extensions**: Leverages core framework utilities for configuration and logging

---

> 💡 **Development Tip**: Use DKNet.Svc.BlobStorage.Local for development, testing, and on-premises scenarios where cloud storage is not needed or available. Always implement proper path validation to prevent directory traversal attacks, use appropriate file permissions, and consider implementing file integrity checks for critical applications. The local adapter is perfect for rapid development iteration and automated testing scenarios.