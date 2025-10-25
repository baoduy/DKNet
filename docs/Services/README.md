# Service Layer

The Service Layer provides application services and cross-cutting concerns that support business operations while maintaining clear separation from domain logic. These services implement the application layer patterns in the Onion Architecture.

## Components

### Blob Storage Services
- [DKNet.Svc.BlobStorage.Abstractions](./DKNet.Svc.BlobStorage.Abstractions.md) - File storage service abstractions
- [DKNet.Svc.BlobStorage.AwsS3](./DKNet.Svc.BlobStorage.AwsS3.md) - AWS S3 storage adapter
- [DKNet.Svc.BlobStorage.AzureStorage](./DKNet.Svc.BlobStorage.AzureStorage.md) - Azure Blob storage adapter
- [DKNet.Svc.BlobStorage.Local](./DKNet.Svc.BlobStorage.Local.md) - Local file system storage

### Data Processing Services
- [DKNet.Svc.Transformation](./DKNet.Svc.Transformation.md) - Data transformation services
- [DKNet.Svc.PdfGenerators](./DKNet.Svc.PdfGenerators.md) - Documentation-grade PDF generation toolkit

### Security & Cryptography
- [DKNet.Svc.Encryption](./DKNet.Svc.Encryption.md) - Cryptographic helpers (AES, RSA, HMAC, hashing)

## Architecture Role in DDD & Onion Architecture

The Service Layer implements the **Application Layer** in the Onion Architecture, orchestrating business operations and providing external integrations:

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 Presentation Layer                        │
│                   (Controllers, API Endpoints)                  │
│                                                                 │
│  Uses: Application services for file uploads, data processing  │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🎯 Application Layer                          │
│              (Use Cases, Application Services)                  │
│                                                                 │
│  🔧 DKNet.Svc.BlobStorage.* - File storage operations          │
│  📊 DKNet.Svc.Transformation - Data processing pipelines       │
│  🎭 Orchestrates domain operations with external services      │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                             │
│           (Entities, Aggregates, Domain Services)              │
│                                                                 │
│  No direct dependencies on service implementations             │
│  May define service contracts/interfaces                       │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🗄️ Infrastructure Layer                        │
│                  (Data Access, External APIs)                  │
│                                                                 │
│  Implements: Concrete service implementations                  │
│  Integrates: AWS S3, Azure Storage, Local file system         │
└─────────────────────────────────────────────────────────────────┘
```

## Key Design Patterns Implemented

### 1. Provider Pattern
- **Abstractions**: Define service contracts independent of implementation
- **Multiple Providers**: Support different storage backends (AWS, Azure, Local)
- **Configuration-Driven**: Select providers through configuration
- **Pluggable Architecture**: Easy to add new providers

### 2. Adapter Pattern
- **Storage Adapters**: Uniform interface for different storage systems
- **Technology Abstraction**: Hide provider-specific implementation details
- **Consistent API**: Same operations across all storage providers
- **Error Handling**: Consistent error handling across providers

### 3. Strategy Pattern
- **Transformation Strategies**: Different algorithms for data processing
- **Runtime Selection**: Choose transformation strategy based on data type
- **Extensible Design**: Easy to add new transformation strategies
- **Pipeline Processing**: Chain multiple transformations together

### 4. Façade Pattern
- **Simplified Interface**: Hide complex subsystem interactions
- **Service Orchestration**: Coordinate multiple operations
- **Cross-Cutting Concerns**: Handle logging, validation, error handling
- **Business-Focused API**: Operations expressed in business terms

## DDD Implementation Benefits

### 1. Application Services
- Orchestrate domain operations without containing business logic
- Handle cross-cutting concerns like file storage and data transformation
- Coordinate between multiple aggregates and external systems
- Maintain transaction boundaries and consistency

### 2. Technology Independence
- Domain layer remains unaware of specific storage or processing technologies
- Easy to switch between different providers based on requirements
- Support for hybrid scenarios (different providers for different use cases)
- Testability through abstraction and mocking

### 3. Cross-Cutting Concerns
- File validation and security handled at service layer
- Data transformation pipelines support business rule implementation
- Monitoring and logging integrated into service operations
- Error handling and retry policies implemented consistently

### 4. Integration Patterns
- Support for event-driven architectures through async operations
- Integration with domain events for file processing workflows
- Support for bulk operations and batch processing scenarios
- Caching and performance optimization at service layer

## Onion Architecture Benefits

### 1. Dependency Inversion
- Application layer defines service contracts
- Infrastructure layer implements concrete services
- Domain layer is protected from external technology concerns
- Easy to mock services for testing

### 2. Separation of Concerns
- File storage separated from business logic
- Data transformation isolated from domain rules
- External integrations handled at appropriate layer
- Clear boundaries between layers

### 3. Testability
- Service abstractions enable comprehensive unit testing
- Integration tests can use different providers (in-memory, local)
- Domain logic can be tested independently of external services
- Easy to simulate various failure scenarios

### 4. Flexibility
- Support for multiple cloud providers simultaneously
- Runtime configuration of storage strategies
- Easy migration between different storage solutions
- Support for multi-tenant scenarios with different storage per tenant

## Service Categories

### 1. File Storage Services
- **Abstract Operations**: Save, retrieve, list, delete, check existence
- **Metadata Management**: File size, content type, modification dates
- **Access Control**: Public URLs, expiration times, permissions
- **Validation**: File size limits, extension restrictions, security checks

### 2. Data Transformation Services
- **Token Processing**: Extract and resolve tokens in templates
- **Format Conversion**: Currency, date-time, boolean transformations
- **Pipeline Processing**: Chainable transformation operations
- **Validation**: Type-safe transformations with error handling

### 3. Cross-Cutting Services
- **Configuration Management**: Provider selection and options
- **Logging and Monitoring**: Comprehensive operation tracking
- **Error Handling**: Consistent error responses and recovery
- **Performance Optimization**: Caching, connection pooling, retry policies

## Integration Patterns

### 1. Domain Event Integration
```csharp
public class DocumentProcessingService
{
    public async Task ProcessDocumentAsync(DocumentUploadedEvent evt)
    {
        // Save document using blob service
        var location = await _blobService.SaveAsync(evt.DocumentData);
        
        // Transform document data
        var transformedData = await _transformationService.ProcessAsync(evt.Metadata);
        
        // Update domain entity
        var document = await _documentRepository.GetByIdAsync(evt.DocumentId);
        document.SetProcessingComplete(location, transformedData);
    }
}
```

### 2. Multi-Provider Configuration
```csharp
public void ConfigureServices(IServiceCollection services)
{
    // Configure multiple storage providers
    services.Configure<BlobServiceOptions>("AWS", options => {
        options.Provider = "S3";
        options.ConnectionString = awsConfig;
    });
    
    services.Configure<BlobServiceOptions>("Azure", options => {
        options.Provider = "AzureBlob";
        options.ConnectionString = azureConfig;
    });
    
    // Register provider factory
    services.AddSingleton<IBlobServiceFactory, BlobServiceFactory>();
}
```

### 3. Pipeline Processing
```csharp
public class DataProcessingPipeline
{
    public async Task<ProcessedData> ProcessAsync(RawData input)
    {
        return await _transformationService
            .ExtractTokens(input)
            .ValidateTokens()
            .ResolveTokens()
            .FormatOutput()
            .ExecuteAsync();
    }
}
```

## Performance & Scalability Features

- **Async Operations**: All service operations support cancellation tokens
- **Streaming Support**: Large file processing with streaming APIs
- **Bulk Operations**: Efficient batch processing capabilities
- **Connection Pooling**: Optimized connections to external services
- **Caching Integration**: Support for distributed caching patterns
- **Retry Policies**: Resilient operations with configurable retry logic

## Security Features

- **File Validation**: Comprehensive file type and size validation
- **Access Control**: Support for temporary URLs and expiration
- **Encryption**: Support for encryption at rest and in transit
- **Audit Logging**: Comprehensive audit trails for compliance
- **Input Sanitization**: Protection against malicious file uploads