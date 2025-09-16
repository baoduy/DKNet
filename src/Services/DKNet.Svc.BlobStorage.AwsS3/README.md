# DKNet.Svc.BlobStorage.AwsS3

[![NuGet](https://img.shields.io/nuget/v/DKNet.Svc.BlobStorage.AwsS3)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.AwsS3/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Svc.BlobStorage.AwsS3)](https://www.nuget.org/packages/DKNet.Svc.BlobStorage.AwsS3/)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../../../LICENSE)

AWS S3 implementation of the DKNet blob storage abstractions, providing seamless integration with Amazon S3 and S3-compatible services including Cloudflare R2 and MinIO. This package offers production-ready blob storage capabilities with AWS-specific optimizations and features.

## Features

- **AWS S3 Integration**: Direct integration with Amazon S3 SDK
- **S3-Compatible Services**: Support for Cloudflare R2, MinIO, and other S3-compatible storage
- **Full IBlobService Implementation**: Complete implementation of DKNet blob storage abstractions
- **Flexible Authentication**: Support for access keys, IAM roles, and connection strings
- **Regional Support**: Support for all AWS regions and custom endpoints
- **Bucket Management**: Automatic bucket creation and management
- **Object Metadata**: Support for custom metadata and properties
- **Streaming Support**: Efficient streaming for large file operations
- **Performance Optimized**: Leverages AWS SDK optimizations and best practices

## Supported Frameworks

- .NET 9.0+
- AWSSDK.S3 3.7+

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package DKNet.Svc.BlobStorage.AwsS3
```

Or via Package Manager Console:

```powershell
Install-Package DKNet.Svc.BlobStorage.AwsS3
```

## Quick Start

### Configuration Setup

```json
{
  "BlobService": {
    "S3": {
      "AccessKey": "your-access-key",
      "Secret": "your-secret-key",
      "RegionEndpointName": "us-east-1",
      "BucketName": "my-bucket",
      "EnableMetadata": true,
      "MaxFileSize": 104857600,
      "GenerateUniqueNames": false,
      "PathPrefix": "uploads/",
      "ForcePathStyle": false,
      "DisablePayloadSigning": false
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
    // Add S3 blob service
    services.AddS3BlobService(configuration);
    
    // Or configure manually
    services.Configure<S3Options>(options =>
    {
        options.AccessKey = "your-access-key";
        options.Secret = "your-secret-key";
        options.RegionEndpointName = "us-east-1";
        options.BucketName = "your-bucket";
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
            Name = $"documents/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}/{file.FileName}",
            ContentStream = stream,
            ContentType = file.ContentType,
            Metadata = new Dictionary<string, string>
            {
                ["original-filename"] = file.FileName,
                ["uploaded-at"] = DateTime.UtcNow.ToString("O"),
                ["file-size"] = file.Length.ToString(),
                ["content-type"] = file.ContentType
            }
        };

        return await _blobService.SaveAsync(blob);
    }

    public async Task<Stream?> DownloadDocumentAsync(string key)
    {
        var request = new BlobRequest(key);
        var result = await _blobService.GetAsync(request);
        
        return result?.ContentStream;
    }
}
```

## Configuration

### S3 Options

```csharp
public class S3Options : BlobServiceOptions
{
    public string AccessKey { get; set; } = null!;
    public string Secret { get; set; } = null!;
    public string RegionEndpointName { get; set; } = "us-east-1";
    public string BucketName { get; set; } = null!;
    public bool ForcePathStyle { get; set; } = false;
    public bool DisablePayloadSigning { get; set; } = false;
    
    // Inherited from BlobServiceOptions
    public bool EnableMetadata { get; set; } = true;
    public long MaxFileSize { get; set; } = 50 * 1024 * 1024; // 50MB
    public string[] AllowedExtensions { get; set; } = Array.Empty<string>();
    public string[] BlockedExtensions { get; set; } = { ".exe", ".bat", ".cmd" };
    public bool GenerateUniqueNames { get; set; } = false;
    public string PathPrefix { get; set; } = string.Empty;
}
```

### AWS Authentication Methods

#### Access Keys (Development)
```csharp
services.Configure<S3Options>(options =>
{
    options.AccessKey = "AKIAIOSFODNN7EXAMPLE";
    options.Secret = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";
    options.RegionEndpointName = "us-east-1";
    options.BucketName = "my-dev-bucket";
});
```

#### IAM Roles (Production)
```csharp
// No explicit credentials needed - uses IAM role
services.Configure<S3Options>(options =>
{
    options.RegionEndpointName = "us-east-1";
    options.BucketName = "my-prod-bucket";
    // AccessKey and Secret will be null - SDK uses IAM role
});
```

#### Environment Variables
```bash
export AWS_ACCESS_KEY_ID=your-access-key
export AWS_SECRET_ACCESS_KEY=your-secret-key
export AWS_DEFAULT_REGION=us-east-1
```

### S3-Compatible Services

#### Cloudflare R2
```csharp
services.Configure<S3Options>(options =>
{
    options.AccessKey = "your-r2-access-key";
    options.Secret = "your-r2-secret-key";
    options.RegionEndpointName = "auto"; // or specific region
    options.BucketName = "your-r2-bucket";
    options.DisablePayloadSigning = true; // Required for R2
    options.ForcePathStyle = true;
    
    // Custom endpoint for R2
    options.ServiceURL = "https://your-account-id.r2.cloudflarestorage.com";
});
```

#### MinIO
```csharp
services.Configure<S3Options>(options =>
{
    options.AccessKey = "minio-access-key";
    options.Secret = "minio-secret-key";
    options.RegionEndpointName = "us-east-1";
    options.BucketName = "my-minio-bucket";
    options.ForcePathStyle = true; // Required for MinIO
    
    // Custom endpoint for MinIO
    options.ServiceURL = "http://localhost:9000";
});
```

## API Reference

### S3BlobService

Implements `IBlobService` with AWS S3 backend:

- `SaveAsync(BlobData, CancellationToken)` - Upload object to S3
- `GetAsync(BlobRequest, CancellationToken)` - Download object from S3
- `GetItemAsync(BlobRequest, CancellationToken)` - Get object metadata only
- `ListItemsAsync(BlobRequest, CancellationToken)` - List objects in bucket
- `DeleteAsync(BlobRequest, CancellationToken)` - Delete object from S3
- `ExistsAsync(BlobRequest, CancellationToken)` - Check if object exists

### S3Options

Configuration class extending `BlobServiceOptions`:

- `AccessKey` - AWS access key ID
- `Secret` - AWS secret access key
- `RegionEndpointName` - AWS region (e.g., "us-east-1")
- `BucketName` - Target S3 bucket name
- `ForcePathStyle` - Use path-style URLs (required for MinIO)
- `DisablePayloadSigning` - Disable payload signing (required for Cloudflare R2)

### Setup Extensions

- `AddS3BlobService(IConfiguration)` - Register S3 implementation

## Advanced Usage

### Pre-signed URLs and Direct Upload

```csharp
public class S3AdvancedService
{
    private readonly IBlobService _blobService;
    private readonly IAmazonS3 _s3Client; // Inject S3 client for advanced operations

    public S3AdvancedService(IBlobService blobService, IAmazonS3 s3Client)
    {
        _blobService = blobService;
        _s3Client = s3Client;
    }

    public async Task<string> GeneratePresignedUploadUrlAsync(string key, TimeSpan expiration)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = "your-bucket",
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiration),
            ContentType = "application/octet-stream"
        };

        return _s3Client.GetPreSignedURL(request);
    }

    public async Task<string> GeneratePresignedDownloadUrlAsync(string key, TimeSpan expiration)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = "your-bucket",
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiration)
        };

        return _s3Client.GetPreSignedURL(request);
    }
}
```

### Multipart Upload for Large Files

```csharp
public class LargeFileUploadService
{
    private readonly IBlobService _blobService;
    private readonly IAmazonS3 _s3Client;
    private readonly S3Options _options;

    public LargeFileUploadService(IBlobService blobService, IAmazonS3 s3Client, IOptions<S3Options> options)
    {
        _blobService = blobService;
        _s3Client = s3Client;
        _options = options.Value;
    }

    public async Task<string> UploadLargeFileAsync(string key, Stream fileStream, IProgress<long>? progress = null)
    {
        const int partSize = 5 * 1024 * 1024; // 5MB parts
        var fileSize = fileStream.Length;
        
        if (fileSize <= partSize)
        {
            // Use regular upload for small files
            var blob = new BlobData
            {
                Name = key,
                ContentStream = fileStream,
                ContentType = "application/octet-stream"
            };
            return await _blobService.SaveAsync(blob);
        }

        // Initialize multipart upload
        var initRequest = new InitiateMultipartUploadRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            ContentType = "application/octet-stream"
        };

        var initResponse = await _s3Client.InitiateMultipartUploadAsync(initRequest);
        var uploadId = initResponse.UploadId;

        try
        {
            var uploadTasks = new List<Task<UploadPartResponse>>();
            var partNumber = 1;
            long uploadedBytes = 0;

            while (fileStream.Position < fileSize)
            {
                var buffer = new byte[partSize];
                var bytesRead = await fileStream.ReadAsync(buffer, 0, partSize);
                
                if (bytesRead == 0) break;

                var partStream = new MemoryStream(buffer, 0, bytesRead);
                var uploadRequest = new UploadPartRequest
                {
                    BucketName = _options.BucketName,
                    Key = key,
                    UploadId = uploadId,
                    PartNumber = partNumber++,
                    InputStream = partStream
                };

                var task = _s3Client.UploadPartAsync(uploadRequest);
                uploadTasks.Add(task);

                uploadedBytes += bytesRead;
                progress?.Report(uploadedBytes);
            }

            var uploadResponses = await Task.WhenAll(uploadTasks);

            // Complete multipart upload
            var completeRequest = new CompleteMultipartUploadRequest
            {
                BucketName = _options.BucketName,
                Key = key,
                UploadId = uploadId,
                PartETags = uploadResponses.Select((response, index) => new PartETag
                {
                    PartNumber = index + 1,
                    ETag = response.ETag
                }).ToList()
            };

            await _s3Client.CompleteMultipartUploadAsync(completeRequest);
            return $"s3://{_options.BucketName}/{key}";
        }
        catch
        {
            // Abort multipart upload on error
            await _s3Client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
            {
                BucketName = _options.BucketName,
                Key = key,
                UploadId = uploadId
            });
            throw;
        }
    }
}
```

### S3 Storage Classes and Lifecycle

```csharp
public class S3StorageManagementService
{
    private readonly IAmazonS3 _s3Client;
    private readonly S3Options _options;

    public S3StorageManagementService(IAmazonS3 s3Client, IOptions<S3Options> options)
    {
        _s3Client = s3Client;
        _options = options.Value;
    }

    public async Task SetStorageClassAsync(string key, S3StorageClass storageClass)
    {
        var request = new CopyObjectRequest
        {
            SourceBucket = _options.BucketName,
            DestinationBucket = _options.BucketName,
            SourceKey = key,
            DestinationKey = key,
            StorageClass = storageClass,
            MetadataDirective = S3MetadataDirective.COPY
        };

        await _s3Client.CopyObjectAsync(request);
    }

    public async Task SetupLifecyclePolicyAsync()
    {
        var configuration = new LifecycleConfiguration
        {
            Rules = new List<LifecycleRule>
            {
                new LifecycleRule
                {
                    Id = "archive-old-files",
                    Status = LifecycleRuleStatus.Enabled,
                    Filter = new LifecycleFilter { Prefix = "uploads/" },
                    Transitions = new List<LifecycleTransition>
                    {
                        new LifecycleTransition
                        {
                            Days = 30,
                            StorageClass = S3StorageClass.StandardInfrequentAccess
                        },
                        new LifecycleTransition
                        {
                            Days = 90,
                            StorageClass = S3StorageClass.Glacier
                        }
                    }
                },
                new LifecycleRule
                {
                    Id = "delete-temporary-files",
                    Status = LifecycleRuleStatus.Enabled,
                    Filter = new LifecycleFilter { Prefix = "temp/" },
                    Expiration = new LifecycleRuleExpiration { Days = 7 }
                }
            }
        };

        var request = new PutLifecycleConfigurationRequest
        {
            BucketName = _options.BucketName,
            Configuration = configuration
        };

        await _s3Client.PutLifecycleConfigurationAsync(request);
    }
}
```

### Cross-Origin Resource Sharing (CORS)

```csharp
public async Task ConfigureCorsAsync()
{
    var corsConfiguration = new CORSConfiguration
    {
        Rules = new List<CORSRule>
        {
            new CORSRule
            {
                Id = "allow-web-uploads",
                AllowedMethods = new List<string> { "GET", "PUT", "POST", "DELETE" },
                AllowedOrigins = new List<string> { "https://yourdomain.com", "https://app.yourdomain.com" },
                AllowedHeaders = new List<string> { "*" },
                MaxAgeSeconds = 3000,
                ExposeHeaders = new List<string> { "ETag" }
            }
        }
    };

    var request = new PutCORSConfigurationRequest
    {
        BucketName = _options.BucketName,
        Configuration = corsConfiguration
    };

    await _s3Client.PutCORSConfigurationAsync(request);
}
```

### Integration with CloudFront CDN

```csharp
public class S3CloudFrontService
{
    private readonly IBlobService _blobService;
    private readonly string _cloudFrontDomain;

    public S3CloudFrontService(IBlobService blobService, IConfiguration configuration)
    {
        _blobService = blobService;
        _cloudFrontDomain = configuration["CloudFront:Domain"];
    }

    public async Task<string> UploadWithCdnUrlAsync(BlobData blob)
    {
        var s3Location = await _blobService.SaveAsync(blob);
        
        // Convert S3 URL to CloudFront URL
        var s3Uri = new Uri(s3Location);
        var key = s3Uri.AbsolutePath.TrimStart('/');
        
        return $"https://{_cloudFrontDomain}/{key}";
    }

    public string GetCdnUrl(string key)
    {
        return $"https://{_cloudFrontDomain}/{key.TrimStart('/')}";
    }
}
```

### Batch Operations

```csharp
public class S3BatchService
{
    private readonly IBlobService _blobService;
    private readonly IAmazonS3 _s3Client;
    private readonly S3Options _options;

    public S3BatchService(IBlobService blobService, IAmazonS3 s3Client, IOptions<S3Options> options)
    {
        _blobService = blobService;
        _s3Client = s3Client;
        _options = options.Value;
    }

    public async Task<List<string>> BatchUploadAsync(IEnumerable<(string key, Stream content, string contentType)> files)
    {
        var uploadTasks = files.Select(async file =>
        {
            try
            {
                var blob = new BlobData
                {
                    Name = file.key,
                    ContentStream = file.content,
                    ContentType = file.contentType
                };

                return await _blobService.SaveAsync(blob);
            }
            catch (Exception ex)
            {
                // Log error but continue with other uploads
                Console.WriteLine($"Failed to upload {file.key}: {ex.Message}");
                return null;
            }
        });

        var results = await Task.WhenAll(uploadTasks);
        return results.Where(r => r != null).Cast<string>().ToList();
    }

    public async Task BatchDeleteAsync(IEnumerable<string> keys)
    {
        const int batchSize = 1000; // S3 delete limit
        var keysList = keys.ToList();

        for (int i = 0; i < keysList.Count; i += batchSize)
        {
            var batch = keysList.Skip(i).Take(batchSize).ToList();
            
            var deleteRequest = new DeleteObjectsRequest
            {
                BucketName = _options.BucketName,
                Objects = batch.Select(key => new KeyVersion { Key = key }).ToList()
            };

            try
            {
                var response = await _s3Client.DeleteObjectsAsync(deleteRequest);
                
                if (response.DeleteErrors.Any())
                {
                    foreach (var error in response.DeleteErrors)
                    {
                        Console.WriteLine($"Failed to delete {error.Key}: {error.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Batch delete failed: {ex.Message}");
            }
        }
    }
}
```

## Error Handling and Resilience

```csharp
public class ResilientS3Service
{
    private readonly IBlobService _blobService;
    private readonly ILogger<ResilientS3Service> _logger;

    public ResilientS3Service(IBlobService blobService, ILogger<ResilientS3Service> logger)
    {
        _blobService = blobService;
        _logger = logger;
    }

    public async Task<string?> UploadWithRetryAsync(BlobData blob, int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await _blobService.SaveAsync(blob);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                if (attempt == maxRetries)
                    throw;

                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt) + Random.Shared.NextDouble());
                _logger.LogWarning("S3 rate limit hit, retrying in {Delay}s (attempt {Attempt}/{MaxRetries})",
                    delay.TotalSeconds, attempt, maxRetries);
                
                await Task.Delay(delay);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
            {
                if (attempt == maxRetries)
                    throw;

                _logger.LogWarning(ex, "S3 server error, retrying (attempt {Attempt}/{MaxRetries})", 
                    attempt, maxRetries);
                
                await Task.Delay(TimeSpan.FromSeconds(attempt));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload failed for {BlobName}", blob.Name);
                throw;
            }
        }

        return null;
    }
}
```

## Performance Considerations

- **Connection Pooling**: AWS SDK handles connection pooling automatically
- **Parallel Operations**: Use `Task.WhenAll` for concurrent operations
- **Streaming**: Always use streams for large files to avoid memory issues
- **Multipart Upload**: Use for files larger than 5MB
- **CloudFront**: Use CloudFront CDN for frequently accessed content

## Security Considerations

- **IAM Roles**: Use IAM roles in production instead of access keys
- **Bucket Policies**: Configure appropriate bucket policies for access control
- **Encryption**: Enable server-side encryption (SSE-S3, SSE-KMS, or SSE-C)
- **VPC Endpoints**: Use VPC endpoints for secure access from AWS resources
- **Pre-signed URLs**: Use pre-signed URLs for secure client-side uploads

## Thread Safety

- AWS S3 SDK is thread-safe
- Service instance can be used concurrently
- Stream operations should not share streams between threads
- S3 client is cached and thread-safe

## Contributing

See the main [CONTRIBUTING.md](../../../../../CONTRIBUTING.md) for guidelines on how to contribute to this project.

## License

This project is licensed under the [MIT License](../../../../../LICENSE).

## Related Packages

- [DKNet.Svc.BlobStorage.Abstractions](../DKNet.Svc.BlobStorage.Abstractions) - Core blob storage abstractions
- [DKNet.Svc.BlobStorage.AzureStorage](../DKNet.Svc.BlobStorage.AzureStorage) - Azure Blob Storage implementation
- [DKNet.Svc.BlobStorage.Local](../DKNet.Svc.BlobStorage.Local) - Local file system implementation

---

Part of the [DKNet Framework](https://github.com/baoduy/DKNet) - A comprehensive .NET framework for building modern, scalable applications.