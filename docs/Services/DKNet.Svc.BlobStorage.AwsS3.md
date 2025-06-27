# DKNet.Svc.BlobStorage.AwsS3

**AWS S3 storage adapter implementation that provides secure, scalable cloud blob storage operations through Amazon Simple Storage Service (S3), implementing the blob storage abstractions defined in DKNet.Svc.BlobStorage.Abstractions while supporting Domain-Driven Design (DDD) and Onion Architecture principles.**

## What is this project?

DKNet.Svc.BlobStorage.AwsS3 provides a complete implementation of the blob storage abstractions for Amazon S3, enabling applications to store, retrieve, and manage files in AWS S3 buckets. This adapter handles all AWS-specific configurations, authentication, and optimizations while providing a consistent interface through the DKNet blob storage abstractions.

### Key Features

- **S3BlobService**: Complete IBlobService implementation for Amazon S3
- **S3Options**: Comprehensive configuration options for S3 connectivity
- **S3Setup**: Streamlined service registration and configuration
- **AWS SDK Integration**: Full integration with AWS SDK for .NET
- **Multi-Region Support**: Support for different AWS regions and endpoints
- **Security**: IAM roles, access keys, and S3 bucket policies integration
- **Performance Optimization**: Multipart uploads, transfer acceleration, and caching
- **Versioning Support**: S3 object versioning and lifecycle management
- **Encryption**: Server-side encryption (SSE-S3, SSE-KMS) support
- **Access Control**: Public/private access with presigned URLs

## How it contributes to DDD and Onion Architecture

### Infrastructure Layer Implementation

DKNet.Svc.BlobStorage.AwsS3 implements the **Infrastructure Layer** of the Onion Architecture, providing concrete AWS S3 storage capabilities without affecting higher layers:

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 Presentation Layer                        │
│                   (Controllers, API Endpoints)                  │
│                                                                 │
│  Uses: File upload/download endpoints with cloud URLs          │
│  Returns: S3 URLs, download streams, upload confirmations      │
└─────────────────────────────┬───────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🎯 Application Layer                          │
│              (Use Cases, Application Services)                  │
│                                                                 │
│  Depends on: IBlobService abstraction                          │
│  Benefits from: Scalable cloud storage, CDN integration        │
│  Orchestrates: File processing workflows with S3               │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                             │
│           (Entities, Aggregates, Domain Services)              │
│                                                                 │
│  🎭 Domain entities reference file locations as value objects  │
│  📝 File metadata as domain concepts (S3 key, CDN URL)         │
│  🏷️ Completely unaware of S3 implementation details           │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🗄️ Infrastructure Layer                        │
│                  (AWS S3 Implementation)                       │
│                                                                 │
│  ☁️ S3BlobService - AWS S3 client integration                 │
│  🔧 S3Options - AWS configuration and authentication          │
│  ⚙️ S3Setup - Service registration and DI setup              │
│  🔒 AWS IAM integration and security policies                 │
│  📊 S3 performance optimizations and monitoring               │
│  🌍 Multi-region support and edge locations                   │
└─────────────────────────────────────────────────────────────────┘
```

### DDD Benefits

1. **Domain Independence**: Domain layer unaware of AWS S3 specifics
2. **Cloud-Native Scalability**: Leverages AWS S3's global infrastructure
3. **Business Continuity**: Reliable cloud storage for business-critical files
4. **Cost Optimization**: S3 lifecycle policies aligned with business retention rules
5. **Global Access**: CDN integration for worldwide file access
6. **Compliance**: AWS compliance features supporting business requirements

### Onion Architecture Benefits

1. **Dependency Inversion**: Infrastructure implements abstractions defined in higher layers
2. **Technology Flexibility**: Easy to switch between cloud providers
3. **Testability**: S3 adapter can be mocked for unit testing
4. **Separation of Concerns**: AWS-specific logic isolated from business logic
5. **Configuration Management**: Centralized AWS configuration
6. **Scalability**: Cloud-native scaling without application changes

## How to use it

### Installation

```bash
dotnet add package DKNet.Svc.BlobStorage.AwsS3
dotnet add package DKNet.Svc.BlobStorage.Abstractions
dotnet add package AWSSDK.S3
```

### Basic Usage Examples

#### 1. Configuration and Setup

```csharp
using DKNet.Svc.BlobStorage.AwsS3;
using DKNet.Svc.BlobStorage.Abstractions;

// appsettings.json configuration
{
  "AWS": {
    "Profile": "default",
    "Region": "us-east-1"
  },
  "S3BlobStorage": {
    "BucketName": "my-application-files",
    "AccessKey": "AKIA...", // Optional if using IAM roles
    "SecretKey": "...", // Optional if using IAM roles
    "Region": "us-east-1",
    "UseHttps": true,
    "EnableTransferAcceleration": true,
    "ServerSideEncryption": "AES256",
    "DefaultExpirationHours": 24,
    "MaxFileSize": 104857600, // 100MB
    "AllowedFileExtensions": [".jpg", ".jpeg", ".png", ".pdf", ".docx"],
    "PublicReadAccess": false
  }
}

// Service registration
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddS3BlobStorage(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Configure S3 options
        services.Configure<S3Options>(configuration.GetSection("S3BlobStorage"));
        
        // Register AWS services
        services.AddAWSService<IAmazonS3>();
        
        // Register blob storage service
        services.AddScoped<IBlobService, S3BlobService>();
        
        return services;
    }
    
    // Alternative with explicit configuration
    public static IServiceCollection AddS3BlobStorageWithOptions(
        this IServiceCollection services,
        Action<S3Options> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddAWSService<IAmazonS3>();
        services.AddScoped<IBlobService, S3BlobService>();
        
        return services;
    }
}

// Program.cs or Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Method 1: Configuration-based setup
    services.AddS3BlobStorage(Configuration);
    
    // Method 2: Explicit options setup
    services.AddS3BlobStorageWithOptions(options =>
    {
        options.BucketName = "my-app-files";
        options.Region = "us-west-2";
        options.UseHttps = true;
        options.EnableTransferAcceleration = true;
        options.ServerSideEncryption = "aws:kms";
        options.KmsKeyId = "alias/my-app-key";
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
    
    // Upload document to S3
    public async Task<string> UploadDocumentAsync(IFormFile file, string userId)
    {
        try
        {
            var fileName = $"{userId}/documents/{Guid.NewGuid()}-{file.FileName}";
            
            using var stream = file.OpenReadStream();
            var blobData = new BlobData
            {
                FileName = fileName,
                ContentType = file.ContentType,
                Content = stream,
                Metadata = new Dictionary<string, string>
                {
                    ["UploadedBy"] = userId,
                    ["OriginalFileName"] = file.FileName,
                    ["UploadedAt"] = DateTime.UtcNow.ToString("O")
                }
            };
            
            var result = await _blobService.SaveAsync(blobData);
            
            _logger.LogInformation("Document uploaded successfully: {FileName} -> {S3Key}", 
                file.FileName, result.FileName);
            
            return result.FileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload document: {FileName}", file.FileName);
            throw;
        }
    }
    
    // Download document from S3
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
            
            return new FileStreamResult(blobData.Content, blobData.ContentType)
            {
                FileDownloadName = Path.GetFileName(fileName)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download document: {FileName}", fileName);
            throw;
        }
    }
    
    // Get public URL for document
    public async Task<string> GetDocumentUrlAsync(string fileName, TimeSpan? expiration = null)
    {
        var expirationTime = expiration ?? TimeSpan.FromHours(1);
        return await _blobService.GetPublicUrlAsync(fileName, expirationTime);
    }
    
    // List user documents
    public async Task<IEnumerable<BlobInfo>> GetUserDocumentsAsync(string userId)
    {
        var prefix = $"{userId}/documents/";
        return await _blobService.ListAsync(prefix);
    }
    
    // Delete document
    public async Task DeleteDocumentAsync(string fileName)
    {
        try
        {
            var exists = await _blobService.ExistsAsync(fileName);
            if (exists)
            {
                await _blobService.DeleteAsync(fileName);
                _logger.LogInformation("Document deleted successfully: {FileName}", fileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document: {FileName}", fileName);
            throw;
        }
    }
}
```

#### 3. Advanced S3 Features

```csharp
public class AdvancedS3Service
{
    private readonly IBlobService _blobService;
    private readonly S3Options _options;
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<AdvancedS3Service> _logger;
    
    public AdvancedS3Service(
        IBlobService blobService,
        IOptions<S3Options> options,
        IAmazonS3 s3Client,
        ILogger<AdvancedS3Service> logger)
    {
        _blobService = blobService;
        _options = options.Value;
        _s3Client = s3Client;
        _logger = logger;
    }
    
    // Multipart upload for large files
    public async Task<string> UploadLargeFileAsync(Stream fileStream, string fileName, string contentType)
    {
        const int partSize = 5 * 1024 * 1024; // 5MB parts
        
        try
        {
            var initiateRequest = new InitiateMultipartUploadRequest
            {
                BucketName = _options.BucketName,
                Key = fileName,
                ContentType = contentType,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
            };
            
            var initiateResponse = await _s3Client.InitiateMultipartUploadAsync(initiateRequest);
            var uploadId = initiateResponse.UploadId;
            
            var partETags = new List<PartETag>();
            var partNumber = 1;
            var buffer = new byte[partSize];
            
            while (true)
            {
                var bytesRead = await fileStream.ReadAsync(buffer, 0, partSize);
                if (bytesRead == 0) break;
                
                using var partStream = new MemoryStream(buffer, 0, bytesRead);
                
                var uploadRequest = new UploadPartRequest
                {
                    BucketName = _options.BucketName,
                    Key = fileName,
                    UploadId = uploadId,
                    PartNumber = partNumber,
                    PartSize = bytesRead,
                    InputStream = partStream
                };
                
                var uploadResponse = await _s3Client.UploadPartAsync(uploadRequest);
                partETags.Add(new PartETag(partNumber, uploadResponse.ETag));
                
                _logger.LogDebug("Uploaded part {PartNumber} for file {FileName}", partNumber, fileName);
                partNumber++;
            }
            
            var completeRequest = new CompleteMultipartUploadRequest
            {
                BucketName = _options.BucketName,
                Key = fileName,
                UploadId = uploadId,
                PartETags = partETags
            };
            
            await _s3Client.CompleteMultipartUploadAsync(completeRequest);
            
            _logger.LogInformation("Large file upload completed: {FileName} ({Parts} parts)", 
                fileName, partETags.Count);
            
            return fileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Multipart upload failed for file: {FileName}", fileName);
            
            // Cleanup incomplete upload
            try
            {
                await _s3Client.AbortMultipartUploadAsync(_options.BucketName, fileName, uploadId);
            }
            catch
            {
                // Ignore cleanup errors
            }
            
            throw;
        }
    }
    
    // Set up S3 lifecycle policies
    public async Task ConfigureLifecyclePolicyAsync()
    {
        var lifecycleConfig = new LifecycleConfiguration
        {
            Rules = new List<LifecycleRule>
            {
                new LifecycleRule
                {
                    Id = "TempFilesCleanup",
                    Status = LifecycleRuleStatus.Enabled,
                    Filter = new LifecycleFilter
                    {
                        Prefix = "temp/"
                    },
                    Expiration = new LifecycleRuleExpiration
                    {
                        Days = 7
                    }
                },
                new LifecycleRule
                {
                    Id = "ArchiveOldFiles",
                    Status = LifecycleRuleStatus.Enabled,
                    Filter = new LifecycleFilter
                    {
                        Prefix = "archives/"
                    },
                    Transitions = new List<LifecycleTransition>
                    {
                        new LifecycleTransition
                        {
                            Days = 30,
                            StorageClass = S3StorageClass.StandardInfrequentAccess
                        },
                        new LifecycleTransition
                        {
                            Days = 365,
                            StorageClass = S3StorageClass.Glacier
                        }
                    }
                }
            }
        };
        
        await _s3Client.PutLifecycleConfigurationAsync(_options.BucketName, lifecycleConfig);
        _logger.LogInformation("S3 lifecycle policies configured successfully");
    }
    
    // Enable S3 event notifications
    public async Task ConfigureEventNotificationsAsync(string snsTopicArn)
    {
        var notificationConfig = new NotificationConfiguration
        {
            TopicConfigurations = new List<TopicConfiguration>
            {
                new TopicConfiguration
                {
                    Topic = snsTopicArn,
                    Events = new List<EventType> { EventType.ObjectCreatedAll, EventType.ObjectRemovedAll }
                }
            }
        };
        
        await _s3Client.PutBucketNotificationAsync(_options.BucketName, notificationConfig);
        _logger.LogInformation("S3 event notifications configured for SNS topic: {TopicArn}", snsTopicArn);
    }
    
    // Cross-region replication setup
    public async Task ConfigureCrossRegionReplicationAsync(string destinationBucketArn, string roleArn)
    {
        var replicationConfig = new ReplicationConfiguration
        {
            Role = roleArn,
            Rules = new List<ReplicationRule>
            {
                new ReplicationRule
                {
                    Id = "ReplicateAll",
                    Status = ReplicationRuleStatus.Enabled,
                    Prefix = "",
                    Destination = new ReplicationDestination
                    {
                        BucketArn = destinationBucketArn,
                        StorageClass = S3StorageClass.StandardInfrequentAccess
                    }
                }
            }
        };
        
        await _s3Client.PutBucketReplicationAsync(_options.BucketName, replicationConfig);
        _logger.LogInformation("Cross-region replication configured to: {DestinationBucket}", destinationBucketArn);
    }
}
```

#### 4. Image Processing with S3

```csharp
public class ImageService
{
    private readonly IBlobService _blobService;
    private readonly ILogger<ImageService> _logger;
    
    public ImageService(IBlobService blobService, ILogger<ImageService> logger)
    {
        _blobService = blobService;
        _logger = logger;
    }
    
    // Upload and process images
    public async Task<ImageUploadResult> UploadImageAsync(IFormFile imageFile, string userId)
    {
        if (!IsValidImageFile(imageFile))
        {
            throw new ArgumentException("Invalid image file format");
        }
        
        var imageId = Guid.NewGuid().ToString();
        var baseKey = $"{userId}/images/{imageId}";
        
        var uploadTasks = new List<Task<string>>();
        
        // Upload original image
        using var originalStream = imageFile.OpenReadStream();
        var originalKey = $"{baseKey}/original{Path.GetExtension(imageFile.FileName)}";
        uploadTasks.Add(UploadImageVariantAsync(originalStream, originalKey, imageFile.ContentType));
        
        // Create and upload thumbnails
        var thumbnailSizes = new[] { 150, 300, 800 };
        foreach (var size in thumbnailSizes)
        {
            var thumbnailStream = await CreateThumbnailAsync(imageFile.OpenReadStream(), size);
            var thumbnailKey = $"{baseKey}/thumb_{size}{Path.GetExtension(imageFile.FileName)}";
            uploadTasks.Add(UploadImageVariantAsync(thumbnailStream, thumbnailKey, imageFile.ContentType));
        }
        
        var uploadedKeys = await Task.WhenAll(uploadTasks);
        
        return new ImageUploadResult
        {
            ImageId = imageId,
            OriginalUrl = await _blobService.GetPublicUrlAsync(uploadedKeys[0]),
            ThumbnailUrls = uploadedKeys.Skip(1).Select(key => 
                new { Size = ExtractSizeFromKey(key), Url = _blobService.GetPublicUrlAsync(key).Result })
                .ToDictionary(x => x.Size, x => x.Url)
        };
    }
    
    private async Task<string> UploadImageVariantAsync(Stream imageStream, string key, string contentType)
    {
        var blobData = new BlobData
        {
            FileName = key,
            ContentType = contentType,
            Content = imageStream,
            Metadata = new Dictionary<string, string>
            {
                ["ProcessedAt"] = DateTime.UtcNow.ToString("O"),
                ["ImageVariant"] = ExtractVariantFromKey(key)
            }
        };
        
        var result = await _blobService.SaveAsync(blobData);
        return result.FileName;
    }
    
    private async Task<Stream> CreateThumbnailAsync(Stream originalStream, int size)
    {
        // Image processing logic (using ImageSharp, SkiaSharp, etc.)
        // This is a placeholder for actual image processing
        var thumbnailData = new byte[1024]; // Placeholder
        return new MemoryStream(thumbnailData);
    }
    
    private bool IsValidImageFile(IFormFile file)
    {
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        return allowedTypes.Contains(file.ContentType?.ToLower());
    }
    
    private int ExtractSizeFromKey(string key)
    {
        var match = System.Text.RegularExpressions.Regex.Match(key, @"thumb_(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }
    
    private string ExtractVariantFromKey(string key)
    {
        if (key.Contains("/original")) return "original";
        if (key.Contains("/thumb_")) return $"thumbnail_{ExtractSizeFromKey(key)}";
        return "unknown";
    }
}

public class ImageUploadResult
{
    public string ImageId { get; set; }
    public string OriginalUrl { get; set; }
    public Dictionary<int, string> ThumbnailUrls { get; set; }
}
```

#### 5. S3 Performance Monitoring

```csharp
public class S3PerformanceMonitor
{
    private readonly IAmazonS3 _s3Client;
    private readonly S3Options _options;
    private readonly ILogger<S3PerformanceMonitor> _logger;
    private readonly IMetricsCollector _metricsCollector;
    
    public S3PerformanceMonitor(
        IAmazonS3 s3Client,
        IOptions<S3Options> options,
        ILogger<S3PerformanceMonitor> logger,
        IMetricsCollector metricsCollector)
    {
        _s3Client = s3Client;
        _options = options.Value;
        _logger = logger;
        _metricsCollector = metricsCollector;
    }
    
    // Monitor S3 operation performance
    public async Task<T> MonitorOperationAsync<T>(Func<Task<T>> operation, string operationType)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await operation();
            
            stopwatch.Stop();
            await _metricsCollector.RecordS3OperationAsync(operationType, stopwatch.Elapsed, true);
            
            _logger.LogDebug("S3 {OperationType} completed in {ElapsedMs}ms", 
                operationType, stopwatch.ElapsedMilliseconds);
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await _metricsCollector.RecordS3OperationAsync(operationType, stopwatch.Elapsed, false);
            
            _logger.LogError(ex, "S3 {OperationType} failed after {ElapsedMs}ms", 
                operationType, stopwatch.ElapsedMilliseconds);
            
            throw;
        }
    }
    
    // Get S3 bucket metrics
    public async Task<S3BucketMetrics> GetBucketMetricsAsync()
    {
        var metrics = new S3BucketMetrics();
        
        // Get bucket size and object count
        var listRequest = new ListObjectsV2Request
        {
            BucketName = _options.BucketName,
            MaxKeys = 1000
        };
        
        do
        {
            var response = await _s3Client.ListObjectsV2Async(listRequest);
            
            metrics.ObjectCount += response.KeyCount;
            metrics.TotalSize += response.S3Objects.Sum(obj => obj.Size);
            
            listRequest.ContinuationToken = response.NextContinuationToken;
        }
        while (listRequest.ContinuationToken != null);
        
        // Get bucket location
        var locationResponse = await _s3Client.GetBucketLocationAsync(_options.BucketName);
        metrics.Region = locationResponse.Location.Value;
        
        return metrics;
    }
}

public class S3BucketMetrics
{
    public int ObjectCount { get; set; }
    public long TotalSize { get; set; }
    public string Region { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    
    public string FormattedSize => FormatBytes(TotalSize);
    
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

### Advanced Usage Examples

#### 1. S3 with CDN Integration

```csharp
public class S3CdnService
{
    private readonly IBlobService _blobService;
    private readonly S3Options _options;
    private readonly string _cdnDomain;
    
    public S3CdnService(IBlobService blobService, IOptions<S3Options> options, IConfiguration configuration)
    {
        _blobService = blobService;
        _options = options.Value;
        _cdnDomain = configuration.GetValue<string>("CDN:Domain");
    }
    
    public async Task<string> UploadWithCdnAsync(Stream content, string fileName, string contentType)
    {
        var blobData = new BlobData
        {
            FileName = fileName,
            ContentType = contentType,
            Content = content,
            Metadata = new Dictionary<string, string>
            {
                ["CacheControl"] = "public, max-age=31536000", // 1 year
                ["CDNEnabled"] = "true"
            }
        };
        
        await _blobService.SaveAsync(blobData);
        
        // Return CDN URL instead of S3 URL
        return $"https://{_cdnDomain}/{fileName}";
    }
}
```

#### 2. S3 Backup Service

```csharp
public class S3BackupService
{
    private readonly IAmazonS3 _s3Client;
    private readonly S3Options _options;
    private readonly ILogger<S3BackupService> _logger;
    
    public async Task BackupToS3Async(string localPath, string s3Key)
    {
        using var fileStream = File.OpenRead(localPath);
        
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = $"backups/{DateTime.UtcNow:yyyy/MM/dd}/{s3Key}",
            InputStream = fileStream,
            StorageClass = S3StorageClass.StandardInfrequentAccess,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
        };
        
        await _s3Client.PutObjectAsync(request);
        _logger.LogInformation("Backup completed: {LocalPath} -> {S3Key}", localPath, request.Key);
    }
}
```

## Best Practices

### 1. Security Configuration

```csharp
// Good: Use IAM roles and proper encryption
services.Configure<S3Options>(options =>
{
    options.UseIAMRole = true; // Prefer IAM roles over access keys
    options.ServerSideEncryption = "aws:kms";
    options.KmsKeyId = "alias/app-encryption-key";
    options.PublicReadAccess = false;
});

// Avoid: Hardcoded credentials
services.Configure<S3Options>(options =>
{
    options.AccessKey = "AKIA..."; // Don't hardcode
    options.SecretKey = "..."; // Don't hardcode
});
```

### 2. Performance Optimization

```csharp
// Good: Use appropriate storage classes and lifecycle policies
public async Task UploadArchiveAsync(Stream content, string fileName)
{
    var blobData = new BlobData
    {
        FileName = $"archives/{fileName}",
        Content = content,
        StorageClass = S3StorageClass.StandardInfrequentAccess
    };
    
    await _blobService.SaveAsync(blobData);
}
```

### 3. Error Handling

```csharp
// Good: Proper S3-specific error handling
public async Task<BlobData> GetFileWithRetryAsync(string fileName)
{
    var retryPolicy = Policy
        .Handle<AmazonS3Exception>(ex => ex.StatusCode == HttpStatusCode.ServiceUnavailable)
        .Or<TaskCanceledException>()
        .WaitAndRetryAsync(3, retryAttempt => 
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    
    return await retryPolicy.ExecuteAsync(() => _blobService.GetAsync(fileName));
}
```

## Integration with Other DKNet Components

DKNet.Svc.BlobStorage.AwsS3 integrates seamlessly with other DKNet components:

- **DKNet.Svc.BlobStorage.Abstractions**: Implements the core blob storage contracts
- **DKNet.EfCore.Events**: Supports file-related domain events (FileUploaded, FileDeleted)
- **DKNet.EfCore.Hooks**: Enables file operation hooks and auditing
- **DKNet.SlimBus.Extensions**: Integrates with CQRS for file processing workflows
- **DKNet.Fw.Extensions**: Leverages core framework utilities

---

> 💡 **Cloud Tip**: Use DKNet.Svc.BlobStorage.AwsS3 to leverage AWS S3's enterprise-grade features like global replication, lifecycle management, and security. Always use IAM roles instead of access keys in production, configure appropriate bucket policies, and implement proper monitoring and cost optimization strategies. Consider using S3 Transfer Acceleration for global applications and CloudFront CDN for better performance.