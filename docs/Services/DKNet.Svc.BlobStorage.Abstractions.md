# DKNet.Svc.BlobStorage.Abstractions

**File storage service abstractions that provide a unified interface for blob storage operations across different cloud providers and storage systems, supporting secure file management in Domain-Driven Design applications.**

## What is this project?

DKNet.Svc.BlobStorage.Abstractions defines the core contracts and abstractions for blob storage operations within the DKNet framework. It provides a technology-agnostic interface for file storage operations that can be implemented by various storage providers including AWS S3, Azure Blob Storage, and local file systems.

### Key Features

- **IBlobService**: Unified interface for all blob storage operations
- **BlobData/BlobResult**: Strongly-typed data models for blob operations
- **BlobServiceOptions**: Configurable file validation and security options
- **Content Type Detection**: Automatic MIME type detection based on file extensions
- **Async Operations**: Full async/await support with cancellation tokens
- **Validation Framework**: Built-in file size, extension, and name validation
- **Public Access URLs**: Support for generating time-limited public access URLs
- **Stream Support**: Efficient handling of large files through streaming

## How it contributes to DDD and Onion Architecture

### Application Layer Service Contract

DKNet.Svc.BlobStorage.Abstractions operates in the **Application Layer** of the Onion Architecture, providing contracts that enable file storage operations without coupling to specific technologies:

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 Presentation Layer                        │
│                   (Controllers, API Endpoints)                  │
│                                                                 │
│  Uses: File upload endpoints, download operations              │
│  Returns: File URLs, upload status, download streams           │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🎯 Application Layer                          │
│              (Use Cases, Application Services)                  │
│                                                                 │
│  📄 IBlobService - File storage contract                       │
│  🔧 Document processing, file validation workflows             │
│  📊 Integration with domain aggregates for file management     │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                             │
│           (Entities, Aggregates, Domain Services)              │
│                                                                 │
│  🎭 Domain entities may reference file locations               │
│  📝 File metadata as value objects (file path, content type)   │
│  🏷️ No direct dependency on storage abstractions              │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🗄️ Infrastructure Layer                        │
│                  (Storage Providers, External APIs)            │
│                                                                 │
│  🗃️ AWS S3 Implementation (DKNet.Svc.BlobStorage.AwsS3)       │
│  ☁️ Azure Storage Implementation (DKNet.Svc.BlobStorage.Azure) │
│  💾 Local Storage Implementation (DKNet.Svc.BlobStorage.Local) │
└─────────────────────────────────────────────────────────────────┘
```

### DDD Benefits

1. **Aggregate File Management**: Aggregates can manage file references without storage concerns
2. **Domain Events**: File operations can trigger domain events (DocumentUploaded, FileProcessed)
3. **Value Objects**: File metadata (path, content type, size) as immutable value objects
4. **Policy Enforcement**: File validation rules aligned with business policies
5. **Ubiquitous Language**: File operations expressed in business terms (documents, attachments, assets)

### Onion Architecture Benefits

1. **Dependency Inversion**: Application layer defines contracts, infrastructure implements them
2. **Technology Independence**: Switch between storage providers without changing business logic
3. **Testability**: Mock IBlobService for unit testing file-related operations
4. **Separation of Concerns**: File storage separated from business rules and presentation
5. **Pluggability**: Multiple storage providers can be used simultaneously

## How to use it

### Installation

```bash
dotnet add package DKNet.Svc.BlobStorage.Abstractions
```

### Basic Usage Examples

#### 1. File Upload Operation

```csharp
using DKNet.Svc.BlobStorage.Abstractions;

public class DocumentService
{
    private readonly IBlobService _blobService;
    private readonly IDocumentRepository _documentRepository;
    
    public DocumentService(IBlobService blobService, IDocumentRepository documentRepository)
    {
        _blobService = blobService;
        _documentRepository = documentRepository;
    }
    
    public async Task<Result<DocumentDto>> UploadDocumentAsync(
        UploadDocumentRequest request, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Create blob data from upload request
            var blobData = new BlobData(
                Name: $"documents/{Guid.NewGuid()}/{request.FileName}",
                Data: BinaryData.FromStream(request.FileStream))
            {
                ContentType = request.ContentType,
                Overwrite = false
            };
            
            // Save file to storage
            var fileLocation = await _blobService.SaveAsync(blobData, cancellationToken);
            
            // Create domain entity
            var document = Document.Create(
                name: request.FileName,
                contentType: request.ContentType,
                size: request.FileStream.Length,
                storageLocation: fileLocation,
                uploadedBy: request.UserId);
            
            // Save to repository
            _documentRepository.Add(document);
            await _documentRepository.SaveChangesAsync(cancellationToken);
            
            return Result<DocumentDto>.Success(MapToDto(document));
        }
        catch (Exception ex)
        {
            return Result<DocumentDto>.Failure($"Failed to upload document: {ex.Message}");
        }
    }
}
```

#### 2. File Download Operation

```csharp
public class DocumentDownloadService
{
    private readonly IBlobService _blobService;
    private readonly IDocumentRepository _documentRepository;
    
    public DocumentDownloadService(IBlobService blobService, IDocumentRepository documentRepository)
    {
        _blobService = blobService;
        _documentRepository = documentRepository;
    }
    
    public async Task<Result<FileDownloadResult>> DownloadDocumentAsync(
        Guid documentId, 
        CancellationToken cancellationToken = default)
    {
        // Get document from domain
        var document = await _documentRepository.FindAsync(documentId);
        if (document == null)
            return Result<FileDownloadResult>.Failure("Document not found");
        
        // Check if user has access (domain business rule)
        if (!document.CanBeAccessedBy(currentUserId))
            return Result<FileDownloadResult>.Failure("Access denied");
        
        // Get file from storage
        var blobRequest = new BlobRequest(document.StorageLocation);
        var blobResult = await _blobService.GetAsync(blobRequest, cancellationToken);
        
        if (blobResult == null)
            return Result<FileDownloadResult>.Failure("File not found in storage");
        
        // Update download metrics (domain operation)
        document.RecordDownload(currentUserId);
        await _documentRepository.SaveChangesAsync(cancellationToken);
        
        return Result<FileDownloadResult>.Success(new FileDownloadResult(
            FileName: document.Name,
            ContentType: document.ContentType,
            Data: blobResult.Data,
            Size: blobResult.Data.ToArray().LongLength));
    }
}
```

#### 3. File Listing and Management

```csharp
public class DocumentManagementService
{
    private readonly IBlobService _blobService;
    private readonly IDocumentRepository _documentRepository;
    
    public async Task<IEnumerable<DocumentSummary>> ListUserDocumentsAsync(
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        // Get documents from domain repository
        var userDocuments = await _documentRepository.GetDocumentsByUserAsync(userId);
        
        var summaries = new List<DocumentSummary>();
        
        foreach (var document in userDocuments)
        {
            // Get file metadata from storage
            var blobRequest = new BlobRequest(document.StorageLocation);
            var blobItem = await _blobService.GetItemAsync(blobRequest, cancellationToken);
            
            summaries.Add(new DocumentSummary(
                Id: document.Id,
                Name: document.Name,
                Size: blobItem?.Details?.ContentLength ?? 0,
                LastModified: blobItem?.Details?.LastModified ?? document.CreatedOn.DateTime,
                ContentType: document.ContentType,
                DownloadCount: document.DownloadCount));
        }
        
        return summaries;
    }
    
    public async Task<Result> DeleteDocumentAsync(
        Guid documentId, 
        CancellationToken cancellationToken = default)
    {
        // Domain operation
        var document = await _documentRepository.FindAsync(documentId);
        if (document == null)
            return Result.Failure("Document not found");
        
        if (!document.CanBeDeletedBy(currentUserId))
            return Result.Failure("Cannot delete document");
        
        try
        {
            // Delete from storage
            var blobRequest = new BlobRequest(document.StorageLocation);
            var deleted = await _blobService.DeleteAsync(blobRequest, cancellationToken);
            
            if (deleted)
            {
                // Remove from domain
                _documentRepository.Delete(document);
                await _documentRepository.SaveChangesAsync(cancellationToken);
                
                return Result.Success();
            }
            
            return Result.Failure("Failed to delete file from storage");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to delete document: {ex.Message}");
        }
    }
}
```

#### 4. Public Access URL Generation

```csharp
public class DocumentSharingService
{
    private readonly IBlobService _blobService;
    private readonly IDocumentRepository _documentRepository;
    
    public async Task<Result<string>> GeneratePublicLinkAsync(
        Guid documentId, 
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        // Domain business rule validation
        var document = await _documentRepository.FindAsync(documentId);
        if (document == null)
            return Result<string>.Failure("Document not found");
        
        if (!document.CanBeSharedBy(currentUserId))
            return Result<string>.Failure("Document cannot be shared");
        
        if (!document.IsPublicSharingAllowed())
            return Result<string>.Failure("Public sharing not allowed for this document type");
        
        try
        {
            // Generate public access URL
            var blobRequest = new BlobRequest(document.StorageLocation);
            var publicUrl = await _blobService.GetPublicAccessUrl(
                blobRequest, 
                expiry, 
                cancellationToken);
            
            // Record sharing activity (domain event)
            document.RecordPublicShare(currentUserId, expiry);
            await _documentRepository.SaveChangesAsync(cancellationToken);
            
            return Result<string>.Success(publicUrl.ToString());
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Failed to generate public link: {ex.Message}");
        }
    }
}
```

### Advanced Usage Patterns

#### 1. File Validation Configuration

```csharp
using Microsoft.Extensions.DependencyInjection;
using DKNet.Svc.BlobStorage.Abstractions;

public void ConfigureServices(IServiceCollection services)
{
    // Configure blob service options
    services.Configure<BlobServiceOptions>(options =>
    {
        options.MaxFileSizeInMb = 50; // 50MB limit
        options.MaxFileNameLength = 255;
        options.IncludedExtensions = new[]
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx",
            ".jpg", ".jpeg", ".png", ".gif",
            ".txt", ".csv", ".json", ".xml"
        };
    });
    
    // Register storage provider
    services.AddScoped<IBlobService, AzureBlobService>();
}
```

#### 2. Multi-Provider Setup

```csharp
public class DocumentStorageService
{
    private readonly IServiceProvider _serviceProvider;
    
    public DocumentStorageService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    private IBlobService GetStorageProvider(DocumentType documentType)
    {
        return documentType switch
        {
            DocumentType.PublicDocument => _serviceProvider.GetKeyedService<IBlobService>("Public"),
            DocumentType.PrivateDocument => _serviceProvider.GetKeyedService<IBlobService>("Private"),
            DocumentType.ArchiveDocument => _serviceProvider.GetKeyedService<IBlobService>("Archive"),
            _ => _serviceProvider.GetRequiredService<IBlobService>()
        };
    }
    
    public async Task<string> SaveDocumentAsync(Document document, BinaryData data)
    {
        var storageProvider = GetStorageProvider(document.Type);
        
        var blobData = new BlobData(
            Name: GenerateStoragePath(document),
            Data: data)
        {
            ContentType = document.ContentType
        };
        
        return await storageProvider.SaveAsync(blobData);
    }
}
```

#### 3. Stream Processing for Large Files

```csharp
public class LargeFileUploadService
{
    private readonly IBlobService _blobService;
    
    public async Task<Result<string>> UploadLargeFileAsync(
        string fileName, 
        Stream fileStream,
        IProgress<UploadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Process file in chunks for progress reporting
            using var bufferedStream = new MemoryStream();
            
            const int bufferSize = 8192;
            var buffer = new byte[bufferSize];
            var totalBytesRead = 0L;
            var fileSize = fileStream.Length;
            
            int bytesRead;
            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await bufferedStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                totalBytesRead += bytesRead;
                
                // Report progress
                progress?.Report(new UploadProgress(totalBytesRead, fileSize));
                
                cancellationToken.ThrowIfCancellationRequested();
            }
            
            // Create blob data
            bufferedStream.Position = 0;
            var blobData = new BlobData(
                Name: $"large-files/{Guid.NewGuid()}/{fileName}",
                Data: BinaryData.FromStream(bufferedStream));
            
            // Upload to storage
            var location = await _blobService.SaveAsync(blobData, cancellationToken);
            
            return Result<string>.Success(location);
        }
        catch (OperationCanceledException)
        {
            return Result<string>.Failure("Upload was cancelled");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Upload failed: {ex.Message}");
        }
    }
}
```

#### 4. Integration with Domain Events

```csharp
public class DocumentEventHandler : IEventHandler<DocumentUploadedEvent>
{
    private readonly IBlobService _blobService;
    private readonly IDocumentRepository _documentRepository;
    
    public async Task Handle(DocumentUploadedEvent evt)
    {
        // Get document from domain
        var document = await _documentRepository.FindAsync(evt.DocumentId);
        if (document == null) return;
        
        try
        {
            // Verify file exists in storage
            var blobRequest = new BlobRequest(document.StorageLocation);
            var exists = await _blobService.CheckExistsAsync(blobRequest);
            
            if (exists)
            {
                // Get file metadata
                var blobItem = await _blobService.GetItemAsync(blobRequest);
                
                // Update document with storage metadata
                document.UpdateStorageMetadata(
                    size: blobItem?.Details?.ContentLength ?? 0,
                    lastModified: blobItem?.Details?.LastModified ?? DateTime.UtcNow);
                
                // Mark as processed
                document.MarkAsProcessed();
                
                await _documentRepository.SaveChangesAsync();
            }
            else
            {
                // Handle missing file
                document.MarkAsFailed("File not found in storage");
                await _documentRepository.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            document.MarkAsFailed($"Processing failed: {ex.Message}");
            await _documentRepository.SaveChangesAsync();
        }
    }
}
```

## Best Practices

### 1. File Validation
- Always configure appropriate file size and extension restrictions
- Validate files at the service layer before storage operations
- Consider scanning files for malware in high-security environments
- Use content type detection but don't rely on it for security

### 2. Error Handling
- Implement comprehensive error handling for storage operations
- Use cancellation tokens for long-running operations
- Provide meaningful error messages for different failure scenarios
- Consider retry policies for transient failures

### 3. Performance Optimization
- Use streaming for large files to reduce memory usage
- Implement progress reporting for long-running uploads
- Consider compression for text-based files
- Use appropriate content types for browser caching

### 4. Security Considerations
- Never expose direct storage URLs to clients
- Use time-limited public URLs for shared access
- Implement proper access control at the domain level
- Consider encryption for sensitive documents

### 5. Monitoring and Logging
- Log all file operations for audit trails
- Monitor storage usage and costs
- Track file access patterns for optimization
- Alert on failed operations or unusual activity

## Integration with Other DKNet Components

DKNet.Svc.BlobStorage.Abstractions integrates seamlessly with other DKNet components:

- **DKNet.EfCore.Events**: File operations trigger domain events
- **DKNet.EfCore.Repos**: Document metadata stored in domain repositories
- **DKNet.SlimBus.Extensions**: Async file processing through message bus
- **DKNet.Fw.Extensions**: Content type detection and validation utilities

---

> 💡 **Architecture Tip**: Use blob storage abstractions to keep file storage concerns separate from your domain logic. This enables you to switch storage providers based on requirements (cost, performance, compliance) without changing your business rules.