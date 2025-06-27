# DKNet.Svc.BlobStorage.AzureStorage

**Azure Blob Storage adapter implementation that provides secure, enterprise-grade cloud blob storage operations through Microsoft Azure Blob Storage, implementing the blob storage abstractions defined in DKNet.Svc.BlobStorage.Abstractions while supporting Domain-Driven Design (DDD) and Onion Architecture principles.**

## What is this project?

DKNet.Svc.BlobStorage.AzureStorage provides a complete implementation of the blob storage abstractions for Microsoft Azure Blob Storage, enabling applications to store, retrieve, and manage files in Azure Storage containers. This adapter handles all Azure-specific configurations, authentication, and optimizations while providing a consistent interface through the DKNet blob storage abstractions.

### Key Features

- **AzureStorageBlobService**: Complete IBlobService implementation for Azure Blob Storage
- **AzureStorageOptions**: Comprehensive configuration options for Azure Storage connectivity
- **AzureStorageSetup**: Streamlined service registration and configuration
- **Azure SDK Integration**: Full integration with Azure Storage SDK for .NET
- **Multi-Tier Storage**: Support for Hot, Cool, and Archive storage tiers
- **Security**: Azure AD, SAS tokens, and storage account key authentication
- **Performance Optimization**: Block blob uploads, Azure CDN integration, and caching
- **Geo-Replication**: Support for geo-redundant storage options
- **Encryption**: Client-side and server-side encryption support
- **Access Control**: Container and blob-level access controls with SAS URLs

## How it contributes to DDD and Onion Architecture

### Infrastructure Layer Implementation

DKNet.Svc.BlobStorage.AzureStorage implements the **Infrastructure Layer** of the Onion Architecture, providing concrete Azure Blob Storage capabilities without affecting higher layers:

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 Presentation Layer                        │
│                   (Controllers, API Endpoints)                  │
│                                                                 │
│  Uses: File upload/download endpoints with Azure URLs          │
│  Returns: Azure Blob URLs, download streams, upload results    │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🎯 Application Layer                          │
│              (Use Cases, Application Services)                  │
│                                                                 │
│  Depends on: IBlobService abstraction                          │
│  Benefits from: Enterprise-grade storage, compliance features  │
│  Orchestrates: Document workflows with Azure integration       │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                             │
│           (Entities, Aggregates, Domain Services)              │
│                                                                 │
│  🎭 Domain entities reference file locations as value objects  │
│  📝 File metadata as business concepts (container, blob name)  │
│  🏷️ Completely unaware of Azure implementation details         │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🗄️ Infrastructure Layer                        │
│                  (Azure Blob Storage Implementation)           │
│                                                                 │
│  ☁️ AzureStorageBlobService - Azure Blob client integration   │
│  🔧 AzureStorageOptions - Azure configuration and auth        │
│  ⚙️ AzureStorageSetup - Service registration and DI setup     │
│  🔒 Azure AD integration and RBAC policies                    │
│  📊 Azure Monitor integration and diagnostics                 │
│  🌍 Geo-replication and disaster recovery                     │
└─────────────────────────────────────────────────────────────────┘
```

### DDD Benefits

1. **Domain Independence**: Domain layer unaware of Azure Blob Storage specifics
2. **Enterprise Integration**: Deep integration with Microsoft ecosystem
3. **Compliance Ready**: Built-in compliance features for regulated industries
4. **Business Continuity**: Geo-redundant storage and disaster recovery options
5. **Cost Optimization**: Storage tiers aligned with business data lifecycle
6. **Global Scale**: Azure's global infrastructure supporting worldwide operations

### Onion Architecture Benefits

1. **Dependency Inversion**: Infrastructure implements abstractions defined in higher layers
2. **Technology Flexibility**: Easy to switch between cloud providers
3. **Testability**: Azure adapter can be mocked for unit testing
4. **Separation of Concerns**: Azure-specific logic isolated from business logic
5. **Configuration Management**: Centralized Azure configuration
6. **Scalability**: Cloud-native scaling with Azure's enterprise features

## How to use it

### Installation

```bash
dotnet add package DKNet.Svc.BlobStorage.AzureStorage
dotnet add package DKNet.Svc.BlobStorage.Abstractions
dotnet add package Azure.Storage.Blobs
dotnet add package Azure.Identity
```

### Basic Usage Examples

#### 1. Configuration and Setup

```csharp
using DKNet.Svc.BlobStorage.AzureStorage;
using DKNet.Svc.BlobStorage.Abstractions;
using Azure.Storage.Blobs;
using Azure.Identity;

// appsettings.json configuration
{
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=...",
    "ContainerName": "application-files",
    "StorageAccountName": "myaccount",
    "UseAzureAD": false,
    "DefaultAccessTier": "Hot",
    "EnableSecureTransfer": true,
    "AllowBlobPublicAccess": false,
    "DefaultSasExpirationHours": 24,
    "MaxFileSize": 104857600,
    "AllowedFileExtensions": [".jpg", ".jpeg", ".png", ".pdf", ".docx"],
    "CdnEndpoint": "https://mycdn.azureedge.net"
  }
}

// Service registration
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAzureBlobStorage(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Configure Azure Storage options
        services.Configure<AzureStorageOptions>(configuration.GetSection("AzureStorage"));
        
        // Register Azure Blob Service Client
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AzureStorageOptions>>().Value;
            
            if (options.UseAzureAD)
            {
                // Use Azure AD authentication
                var credential = new DefaultAzureCredential();
                var serviceUri = new Uri($"https://{options.StorageAccountName}.blob.core.windows.net");
                return new BlobServiceClient(serviceUri, credential);
            }
            else
            {
                // Use connection string
                return new BlobServiceClient(options.ConnectionString);
            }
        });
        
        // Register blob storage service
        services.AddScoped<IBlobService, AzureStorageBlobService>();
        
        return services;
    }
    
    // Alternative with managed identity
    public static IServiceCollection AddAzureBlobStorageWithManagedIdentity(
        this IServiceCollection services,
        string storageAccountName,
        string containerName)
    {
        services.Configure<AzureStorageOptions>(options =>
        {
            options.StorageAccountName = storageAccountName;
            options.ContainerName = containerName;
            options.UseAzureAD = true;
        });
        
        services.AddSingleton<BlobServiceClient>(provider =>
        {
            var credential = new ManagedIdentityCredential();
            var serviceUri = new Uri($"https://{storageAccountName}.blob.core.windows.net");
            return new BlobServiceClient(serviceUri, credential);
        });
        
        services.AddScoped<IBlobService, AzureStorageBlobService>();
        
        return services;
    }
}

// Program.cs or Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Method 1: Configuration-based setup
    services.AddAzureBlobStorage(Configuration);
    
    // Method 2: Managed Identity setup for Azure-hosted apps
    services.AddAzureBlobStorageWithManagedIdentity("mystorageaccount", "uploads");
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
    
    // Upload document to Azure Blob Storage
    public async Task<DocumentUploadResult> UploadDocumentAsync(IFormFile file, string userId, string documentType)
    {
        try
        {
            var fileName = GenerateFileName(file.FileName, userId, documentType);
            
            using var stream = file.OpenReadStream();
            var blobData = new BlobData
            {
                FileName = fileName,
                ContentType = file.ContentType,
                Content = stream,
                Metadata = new Dictionary<string, string>
                {
                    ["UploadedBy"] = userId,
                    ["DocumentType"] = documentType,
                    ["OriginalFileName"] = file.FileName,
                    ["UploadedAt"] = DateTime.UtcNow.ToString("O"),
                    ["FileSize"] = file.Length.ToString()
                },
                Tags = new Dictionary<string, string>
                {
                    ["Department"] = "HR",
                    ["Classification"] = "Confidential"
                }
            };
            
            var result = await _blobService.SaveAsync(blobData);
            
            _logger.LogInformation("Document uploaded successfully: {FileName} -> {BlobName}", 
                file.FileName, result.FileName);
            
            return new DocumentUploadResult
            {
                DocumentId = ExtractDocumentId(result.FileName),
                BlobName = result.FileName,
                PublicUrl = await _blobService.GetPublicUrlAsync(result.FileName, TimeSpan.FromHours(1)),
                ContentType = file.ContentType,
                Size = file.Length
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload document: {FileName}", file.FileName);
            throw;
        }
    }
    
    // Download document with access control
    public async Task<FileResult> DownloadDocumentAsync(string documentId, string userId)
    {
        try
        {
            var fileName = ResolveFileName(documentId, userId);
            
            var exists = await _blobService.ExistsAsync(fileName);
            if (!exists)
            {
                throw new FileNotFoundException($"Document not found: {documentId}");
            }
            
            // Check access permissions
            await ValidateDocumentAccessAsync(documentId, userId);
            
            var blobData = await _blobService.GetAsync(fileName);
            
            return new FileStreamResult(blobData.Content, blobData.ContentType)
            {
                FileDownloadName = GetOriginalFileName(blobData.Metadata)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download document: {DocumentId} for user: {UserId}", documentId, userId);
            throw;
        }
    }
    
    // Generate secure SAS URL for temporary access
    public async Task<string> GenerateSecureAccessUrlAsync(string documentId, string userId, TimeSpan expiration)
    {
        var fileName = ResolveFileName(documentId, userId);
        await ValidateDocumentAccessAsync(documentId, userId);
        
        return await _blobService.GetPublicUrlAsync(fileName, expiration);
    }
    
    // Move document to different storage tier
    public async Task ArchiveDocumentAsync(string documentId, string userId)
    {
        var fileName = ResolveFileName(documentId, userId);
        
        // Move to Cool or Archive tier for cost optimization
        await MoveToStorageTierAsync(fileName, "Archive");
        
        _logger.LogInformation("Document archived: {DocumentId}", documentId);
    }
    
    private async Task MoveToStorageTierAsync(string fileName, string tier)
    {
        // This would use Azure-specific APIs to change storage tier
        // Implementation depends on the specific Azure Storage SDK methods
    }
    
    private string GenerateFileName(string originalFileName, string userId, string documentType)
    {
        var extension = Path.GetExtension(originalFileName);
        var documentId = Guid.NewGuid().ToString();
        return $"{documentType}/{userId}/{DateTime.UtcNow:yyyy/MM/dd}/{documentId}{extension}";
    }
    
    private async Task ValidateDocumentAccessAsync(string documentId, string userId)
    {
        // Implement your access control logic here
        // This could check database permissions, role-based access, etc.
    }
}

public class DocumentUploadResult
{
    public string DocumentId { get; set; }
    public string BlobName { get; set; }
    public string PublicUrl { get; set; }
    public string ContentType { get; set; }
    public long Size { get; set; }
}
```

#### 3. Advanced Azure Features

```csharp
public class AdvancedAzureStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly AzureStorageOptions _options;
    private readonly ILogger<AdvancedAzureStorageService> _logger;
    
    public AdvancedAzureStorageService(
        BlobServiceClient blobServiceClient,
        IOptions<AzureStorageOptions> options,
        ILogger<AdvancedAzureStorageService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _options = options.Value;
        _logger = logger;
    }
    
    // Upload large files using block blob API
    public async Task<string> UploadLargeFileAsync(Stream fileStream, string fileName, string contentType)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_options.ContainerName);
        var blobClient = containerClient.GetBlobClient(fileName);
        
        try
        {
            // Upload with options for large files
            var uploadOptions = new BlobUploadOptions
            {
                TransferOptions = new StorageTransferOptions
                {
                    InitialTransferSize = 4 * 1024 * 1024, // 4MB
                    MaximumTransferSize = 4 * 1024 * 1024   // 4MB blocks
                },
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                },
                Metadata = new Dictionary<string, string>
                {
                    ["UploadMethod"] = "LargeFile",
                    ["UploadedAt"] = DateTime.UtcNow.ToString("O")
                },
                AccessTier = AccessTier.Hot
            };
            
            await blobClient.UploadAsync(fileStream, uploadOptions);
            
            _logger.LogInformation("Large file uploaded successfully: {FileName}", fileName);
            return fileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload large file: {FileName}", fileName);
            throw;
        }
    }
    
    // Set up blob lifecycle management
    public async Task ConfigureLifecycleManagementAsync()
    {
        // Note: This typically requires Azure Management API, not Storage API
        // This is a conceptual example
        
        var lifecyclePolicy = new
        {
            rules = new[]
            {
                new
                {
                    name = "MoveToArchive",
                    enabled = true,
                    type = "Lifecycle",
                    definition = new
                    {
                        filters = new
                        {
                            blobTypes = new[] { "blockBlob" },
                            prefixMatch = new[] { "archives/" }
                        },
                        actions = new
                        {
                            baseBlob = new
                            {
                                tierToCool = new { daysAfterModificationGreaterThan = 30 },
                                tierToArchive = new { daysAfterModificationGreaterThan = 90 },
                                delete = new { daysAfterModificationGreaterThan = 2555 } // 7 years
                            }
                        }
                    }
                }
            }
        };
        
        _logger.LogInformation("Lifecycle management policy configured");
    }
    
    // Batch operations for multiple files
    public async Task<BatchOperationResult> BatchUploadAsync(IEnumerable<FileUploadRequest> files)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_options.ContainerName);
        var results = new List<BatchOperationItem>();
        
        var uploadTasks = files.Select(async file =>
        {
            try
            {
                var blobClient = containerClient.GetBlobClient(file.FileName);
                
                await blobClient.UploadAsync(file.Content, new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType },
                    Metadata = file.Metadata
                });
                
                return new BatchOperationItem
                {
                    FileName = file.FileName,
                    Success = true,
                    Message = "Upload successful"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload file in batch: {FileName}", file.FileName);
                return new BatchOperationItem
                {
                    FileName = file.FileName,
                    Success = false,
                    Message = ex.Message
                };
            }
        });
        
        var batchResults = await Task.WhenAll(uploadTasks);
        
        return new BatchOperationResult
        {
            TotalFiles = files.Count(),
            SuccessfulUploads = batchResults.Count(r => r.Success),
            FailedUploads = batchResults.Count(r => !r.Success),
            Results = batchResults.ToList()
        };
    }
    
    // Enable Azure Monitor integration
    public async Task ConfigureMonitoringAsync()
    {
        // Configure diagnostics and logging
        var diagnosticsSettings = new
        {
            logs = new[]
            {
                new { category = "StorageRead", enabled = true, retentionPolicy = new { enabled = true, days = 30 } },
                new { category = "StorageWrite", enabled = true, retentionPolicy = new { enabled = true, days = 30 } },
                new { category = "StorageDelete", enabled = true, retentionPolicy = new { enabled = true, days = 90 } }
            },
            metrics = new[]
            {
                new { category = "Transaction", enabled = true, retentionPolicy = new { enabled = true, days = 30 } }
            }
        };
        
        _logger.LogInformation("Azure Monitor integration configured");
    }
    
    // Geo-replication status check
    public async Task<GeoReplicationStatus> CheckGeoReplicationAsync()
    {
        try
        {
            var properties = await _blobServiceClient.GetPropertiesAsync();
            
            return new GeoReplicationStatus
            {
                Status = properties.Value.GeoReplication?.Status.ToString() ?? "Unknown",
                LastSyncTime = properties.Value.GeoReplication?.LastSyncedOn,
                IsEnabled = properties.Value.GeoReplication != null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check geo-replication status");
            throw;
        }
    }
}

public class FileUploadRequest
{
    public string FileName { get; set; }
    public Stream Content { get; set; }
    public string ContentType { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
}

public class BatchOperationResult
{
    public int TotalFiles { get; set; }
    public int SuccessfulUploads { get; set; }
    public int FailedUploads { get; set; }
    public List<BatchOperationItem> Results { get; set; }
}

public class BatchOperationItem
{
    public string FileName { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; }
}

public class GeoReplicationStatus
{
    public string Status { get; set; }
    public DateTimeOffset? LastSyncTime { get; set; }
    public bool IsEnabled { get; set; }
}
```

#### 4. Azure CDN Integration

```csharp
public class AzureCdnService
{
    private readonly IBlobService _blobService;
    private readonly AzureStorageOptions _options;
    private readonly ILogger<AzureCdnService> _logger;
    
    public AzureCdnService(
        IBlobService blobService,
        IOptions<AzureStorageOptions> options,
        ILogger<AzureCdnService> logger)
    {
        _blobService = blobService;
        _options = options.Value;
        _logger = logger;
    }
    
    // Upload optimized for CDN delivery
    public async Task<string> UploadForCdnAsync(Stream content, string fileName, string contentType)
    {
        var blobData = new BlobData
        {
            FileName = fileName,
            ContentType = contentType,
            Content = content,
            Metadata = new Dictionary<string, string>
            {
                ["CacheControl"] = "public, max-age=31536000", // 1 year
                ["CDNOptimized"] = "true",
                ["UploadedAt"] = DateTime.UtcNow.ToString("O")
            }
        };
        
        await _blobService.SaveAsync(blobData);
        
        // Return CDN URL instead of blob URL
        var cdnUrl = GetCdnUrl(fileName);
        _logger.LogInformation("File uploaded for CDN delivery: {FileName} -> {CdnUrl}", fileName, cdnUrl);
        
        return cdnUrl;
    }
    
    // Purge CDN cache
    public async Task PurgeCdnCacheAsync(string fileName)
    {
        // This would typically use Azure CDN Management API
        var cdnUrl = GetCdnUrl(fileName);
        
        // Implement CDN purge logic here
        _logger.LogInformation("CDN cache purged for: {FileName}", fileName);
    }
    
    private string GetCdnUrl(string fileName)
    {
        if (string.IsNullOrEmpty(_options.CdnEndpoint))
        {
            // Fallback to blob URL if CDN not configured
            return $"https://{_options.StorageAccountName}.blob.core.windows.net/{_options.ContainerName}/{fileName}";
        }
        
        return $"{_options.CdnEndpoint.TrimEnd('/')}/{fileName}";
    }
}
```

#### 5. Azure Storage Analytics

```csharp
public class AzureStorageAnalytics
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<AzureStorageAnalytics> _logger;
    
    public AzureStorageAnalytics(
        BlobServiceClient blobServiceClient,
        ILogger<AzureStorageAnalytics> logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }
    
    // Get storage account usage statistics
    public async Task<StorageUsageStatistics> GetUsageStatisticsAsync()
    {
        var containers = new List<ContainerUsage>();
        
        await foreach (var containerItem in _blobServiceClient.GetBlobContainersAsync())
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerItem.Name);
            var containerUsage = new ContainerUsage
            {
                Name = containerItem.Name,
                BlobCount = 0,
                TotalSize = 0
            };
            
            await foreach (var blobItem in containerClient.GetBlobsAsync())
            {
                containerUsage.BlobCount++;
                containerUsage.TotalSize += blobItem.Properties.ContentLength ?? 0;
            }
            
            containers.Add(containerUsage);
        }
        
        return new StorageUsageStatistics
        {
            TotalContainers = containers.Count,
            TotalBlobs = containers.Sum(c => c.BlobCount),
            TotalSize = containers.Sum(c => c.TotalSize),
            Containers = containers,
            GeneratedAt = DateTime.UtcNow
        };
    }
    
    // Analyze blob access patterns
    public async Task<AccessPatternAnalysis> AnalyzeAccessPatternsAsync(string containerName, TimeSpan period)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var analysis = new AccessPatternAnalysis();
        
        await foreach (var blobItem in containerClient.GetBlobsAsync(BlobTraits.Metadata))
        {
            var lastModified = blobItem.Properties.LastModified?.DateTime ?? DateTime.MinValue;
            var daysSinceModification = (DateTime.UtcNow - lastModified).Days;
            
            if (daysSinceModification <= 30)
                analysis.HotTierCandidates++;
            else if (daysSinceModification <= 90)
                analysis.CoolTierCandidates++;
            else
                analysis.ArchiveTierCandidates++;
        }
        
        return analysis;
    }
    
    // Generate cost optimization recommendations
    public async Task<CostOptimizationRecommendations> GenerateRecommendationsAsync()
    {
        var usage = await GetUsageStatisticsAsync();
        var recommendations = new CostOptimizationRecommendations();
        
        foreach (var container in usage.Containers)
        {
            var analysis = await AnalyzeAccessPatternsAsync(container.Name, TimeSpan.FromDays(90));
            
            if (analysis.CoolTierCandidates > 0)
            {
                recommendations.Recommendations.Add($"Consider moving {analysis.CoolTierCandidates} blobs in container '{container.Name}' to Cool tier");
            }
            
            if (analysis.ArchiveTierCandidates > 0)
            {
                recommendations.Recommendations.Add($"Consider moving {analysis.ArchiveTierCandidates} blobs in container '{container.Name}' to Archive tier");
            }
        }
        
        return recommendations;
    }
}

public class StorageUsageStatistics
{
    public int TotalContainers { get; set; }
    public int TotalBlobs { get; set; }
    public long TotalSize { get; set; }
    public List<ContainerUsage> Containers { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    
    public string FormattedTotalSize => FormatBytes(TotalSize);
    
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

public class ContainerUsage
{
    public string Name { get; set; }
    public int BlobCount { get; set; }
    public long TotalSize { get; set; }
}

public class AccessPatternAnalysis
{
    public int HotTierCandidates { get; set; }
    public int CoolTierCandidates { get; set; }
    public int ArchiveTierCandidates { get; set; }
}

public class CostOptimizationRecommendations
{
    public List<string> Recommendations { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
```

### Advanced Usage Examples

#### 1. Azure Key Vault Integration

```csharp
public class SecureAzureStorageService
{
    private readonly IBlobService _blobService;
    private readonly KeyVaultClient _keyVaultClient;
    
    public async Task<string> UploadEncryptedAsync(Stream content, string fileName, string keyVaultKeyId)
    {
        // Encrypt content using Azure Key Vault key
        var encryptedContent = await EncryptContentAsync(content, keyVaultKeyId);
        
        var blobData = new BlobData
        {
            FileName = fileName,
            Content = encryptedContent,
            Metadata = new Dictionary<string, string>
            {
                ["Encrypted"] = "true",
                ["KeyVaultKeyId"] = keyVaultKeyId
            }
        };
        
        return await _blobService.SaveAsync(blobData).ContinueWith(t => t.Result.FileName);
    }
    
    private async Task<Stream> EncryptContentAsync(Stream content, string keyVaultKeyId)
    {
        // Implementation depends on Azure Key Vault SDK
        // This is a placeholder for encryption logic
        return content;
    }
}
```

#### 2. Event Grid Integration

```csharp
public class AzureStorageEventService
{
    private readonly EventGridPublisherClient _eventGridClient;
    private readonly ILogger<AzureStorageEventService> _logger;
    
    public async Task PublishBlobEventAsync(string eventType, string blobName, object eventData)
    {
        var eventGridEvent = new EventGridEvent(
            subject: $"/blobs/{blobName}",
            eventType: eventType,
            dataVersion: "1.0",
            data: eventData);
        
        await _eventGridClient.SendEventAsync(eventGridEvent);
        _logger.LogInformation("Event published: {EventType} for blob: {BlobName}", eventType, blobName);
    }
}
```

## Best Practices

### 1. Security Configuration

```csharp
// Good: Use managed identity and Azure AD
services.AddAzureBlobStorageWithManagedIdentity("mystorageaccount", "uploads");

// Good: Configure proper access tiers
services.Configure<AzureStorageOptions>(options =>
{
    options.DefaultAccessTier = "Hot"; // For frequently accessed files
    options.AllowBlobPublicAccess = false; // Secure by default
    options.EnableSecureTransfer = true; // HTTPS only
});

// Avoid: Hardcoded connection strings in production
services.Configure<AzureStorageOptions>(options =>
{
    options.ConnectionString = "DefaultEndpointsProtocol=https;AccountName=..."; // Don't hardcode
});
```

### 2. Performance Optimization

```csharp
// Good: Use appropriate storage tiers
public async Task UploadArchiveAsync(Stream content, string fileName)
{
    var blobData = new BlobData
    {
        FileName = $"archives/{fileName}",
        Content = content,
        AccessTier = "Archive" // Cost-effective for long-term storage
    };
    
    await _blobService.SaveAsync(blobData);
}

// Good: Configure transfer options for large files
var uploadOptions = new BlobUploadOptions
{
    TransferOptions = new StorageTransferOptions
    {
        InitialTransferSize = 4 * 1024 * 1024,
        MaximumTransferSize = 4 * 1024 * 1024
    }
};
```

### 3. Error Handling

```csharp
// Good: Handle Azure-specific exceptions
public async Task<BlobData> GetFileWithRetryAsync(string fileName)
{
    try
    {
        return await _blobService.GetAsync(fileName);
    }
    catch (RequestFailedException ex) when (ex.Status == 404)
    {
        throw new FileNotFoundException($"Blob not found: {fileName}");
    }
    catch (RequestFailedException ex) when (ex.Status == 403)
    {
        throw new UnauthorizedAccessException($"Access denied to blob: {fileName}");
    }
    catch (RequestFailedException ex)
    {
        _logger.LogError(ex, "Azure Storage error for blob: {FileName}", fileName);
        throw;
    }
}
```

## Integration with Other DKNet Components

DKNet.Svc.BlobStorage.AzureStorage integrates seamlessly with other DKNet components:

- **DKNet.Svc.BlobStorage.Abstractions**: Implements the core blob storage contracts
- **DKNet.EfCore.Events**: Supports file-related domain events (DocumentUploaded, BlobCreated)
- **DKNet.EfCore.Hooks**: Enables file operation hooks and auditing
- **DKNet.SlimBus.Extensions**: Integrates with CQRS for document processing workflows
- **DKNet.Fw.Extensions**: Leverages core framework utilities for configuration and logging

---

> 💡 **Azure Tip**: Use DKNet.Svc.BlobStorage.AzureStorage to leverage Azure's enterprise features like geo-replication, lifecycle management, and deep integration with the Microsoft ecosystem. Always use managed identity in production, configure appropriate access tiers for cost optimization, and implement proper monitoring with Azure Monitor. Consider using Azure CDN for global applications and Event Grid for event-driven architectures.