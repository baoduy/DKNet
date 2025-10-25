# API Reference

Comprehensive API reference for all DKNet Framework components.

## 📋 Table of Contents

- [Core Framework Extensions](#core-framework-extensions)
- [Entity Framework Core](#entity-framework-core)
- [Messaging & CQRS](#messaging--cqrs)
- [Blob Storage Services](#blob-storage-services)
- [Security & Encryption](#-security--encryption)
- [PDF Generation](#-pdf-generation)
- [ASP.NET Core Utilities](#-aspnet-core-utilities)
- [Aspire Integrations](#-aspire-integrations)
- [Common Interfaces](#common-interfaces)
- [Configuration Options](#configuration-options)

---

## 🔧 Core Framework Extensions

### DKNet.Fw.Extensions

#### Type Extensions

```csharp
namespace DKNet.Fw.Extensions;

public static class TypeExtensions
{
    /// <summary>
    /// Checks if a type implements the specified interface.
    /// </summary>
    /// <typeparam name="TInterface">The interface type to check for.</typeparam>
    /// <param name="type">The type to check.</param>
    /// <returns>true if the type implements the interface; otherwise, false.</returns>
    public static bool ImplementsInterface<TInterface>(this Type type);
    
    /// <summary>
    /// Checks if a type implements the specified interface.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <param name="interfaceType">The interface type to check for.</param>
    /// <returns>true if the type implements the interface; otherwise, false.</returns>
    public static bool ImplementsInterface(this Type type, Type interfaceType);
    
    /// <summary>
    /// Gets all interfaces implemented by the type.
    /// </summary>
    /// <param name="type">The type to examine.</param>
    /// <returns>An array of interface types.</returns>
    public static Type[] GetImplementedInterfaces(this Type type);
}
```

#### Property Extensions

```csharp
public static class PropertyExtensions
{
    /// <summary>
    /// Gets the value of a property by name.
    /// </summary>
    /// <param name="obj">The object instance.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The property value or null if not found.</returns>
    public static object? GetPropertyValue(this object? obj, string propertyName);
    
    /// <summary>
    /// Gets the value of a property by name with type conversion.
    /// </summary>
    /// <typeparam name="T">The expected return type.</typeparam>
    /// <param name="obj">The object instance.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The property value converted to T or default(T) if not found.</returns>
    public static T? GetPropertyValue<T>(this object? obj, string propertyName);
    
    /// <summary>
    /// Sets the value of a property by name.
    /// </summary>
    /// <param name="obj">The object instance.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>true if the property was set successfully; otherwise, false.</returns>
    public static bool SetPropertyValue(this object obj, string propertyName, object? value);
}
```

#### Enum Extensions

```csharp
public static class EnumExtensions
{
    /// <summary>
    /// Gets the description attribute value of an enum member.
    /// </summary>
    /// <param name="enumValue">The enum value.</param>
    /// <returns>The description or the enum name if no description is found.</returns>
    public static string GetDescription(this Enum enumValue);
    
    /// <summary>
    /// Gets all description attribute values for an enum type.
    /// </summary>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <returns>A dictionary mapping enum values to their descriptions.</returns>
    public static Dictionary<T, string> GetAllDescriptions<T>() where T : Enum;
    
    /// <summary>
    /// Converts a string to an enum value.
    /// </summary>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <param name="value">The string value.</param>
    /// <param name="ignoreCase">Whether to ignore case when parsing.</param>
    /// <returns>The enum value if parsing succeeds; otherwise, the default value.</returns>
    public static T ToEnum<T>(this string value, bool ignoreCase = true) where T : struct, Enum;
}
```

#### Async Enumerable Extensions

```csharp
public static class AsyncEnumerableExtensions
{
    /// <summary>
    /// Converts an IAsyncEnumerable to a List asynchronously.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The async enumerable source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list containing all elements from the async enumerable.</returns>
    public static async Task<List<T>> ToListAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Filters elements of an async enumerable based on a predicate.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The async enumerable source.</param>
    /// <param name="predicate">The predicate function.</param>
    /// <returns>A filtered async enumerable.</returns>
    public static IAsyncEnumerable<T> WhereAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate);
    
    /// <summary>
    /// Projects each element of an async enumerable to a new form.
    /// </summary>
    /// <typeparam name="TSource">The source element type.</typeparam>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="source">The async enumerable source.</param>
    /// <param name="selector">The selector function.</param>
    /// <returns>A projected async enumerable.</returns>
    public static IAsyncEnumerable<TResult> SelectAsync<TSource, TResult>(
        this IAsyncEnumerable<TSource> source,
        Func<TSource, TResult> selector);
}
```

---

## 🗄️ Entity Framework Core

### DKNet.EfCore.Abstractions

#### Base Entities

```csharp
namespace DKNet.EfCore.Abstractions;

/// <summary>
/// Base class for all entities with auditing support.
/// </summary>
public abstract class AggregateRoot : IAuditable, IDomainEvents
{
    protected AggregateRoot(Guid id, string createdBy)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        CreatedBy = createdBy;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; protected set; }
    public string CreatedBy { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public string? UpdatedBy { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }

    /// <summary>
    /// Sets the user who updated the entity.
    /// </summary>
    /// <param name="updatedBy">The user identifier.</param>
    protected void SetUpdatedBy(string updatedBy)
    {
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    private readonly List<DomainEvent> _domainEvents = new();

    /// <summary>
    /// Adds a domain event to be raised when the entity is saved.
    /// </summary>
    /// <param name="domainEvent">The domain event.</param>
    public void AddEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>
    /// Gets all uncommitted domain events.
    /// </summary>
    /// <returns>A read-only collection of domain events.</returns>
    public IReadOnlyCollection<DomainEvent> GetUncommittedEvents() => _domainEvents.AsReadOnly();

    /// <summary>
    /// Clears all uncommitted domain events.
    /// </summary>
    public void ClearEvents() => _domainEvents.Clear();
}

/// <summary>
/// Interface for tenant-aware entities.
/// </summary>
public interface ITenantEntity
{
    string TenantId { get; set; }
}

/// <summary>
/// Interface for auditable entities.
/// </summary>
public interface IAuditable
{
    string CreatedBy { get; }
    DateTime CreatedAt { get; }
    string? UpdatedBy { get; }
    DateTime? UpdatedAt { get; }
}
```

#### Domain Events

```csharp
/// <summary>
/// Base class for all domain events.
/// </summary>
public abstract record DomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

/// <summary>
/// Interface for domain event handlers.
/// </summary>
/// <typeparam name="TEvent">The domain event type.</typeparam>
public interface IDomainEventHandler<in TEvent> where TEvent : DomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken = default);
}
```

### DKNet.EfCore.Repos

#### Repository Interfaces

```csharp
namespace DKNet.EfCore.Repos.Abstractions;

/// <summary>
/// Generic repository interface for entities.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IRepository<TEntity> where TEntity : class
{
    /// <summary>
    /// Gets a queryable for the entity.
    /// </summary>
    /// <returns>An IQueryable for the entity type.</returns>
    IQueryable<TEntity> Gets();

    /// <summary>
    /// Finds an entity by its identifier.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entity if found; otherwise, null.</returns>
    Task<TEntity?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The added entity.</returns>
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    void Update(TEntity entity);

    /// <summary>
    /// Deletes an entity.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    void Delete(TEntity entity);

    /// <summary>
    /// Finds entities matching a specification.
    /// </summary>
    /// <param name="specification">The specification to apply.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of matching entities.</returns>
    Task<IEnumerable<TEntity>> FindAsync(
        Specification<TEntity> specification,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only repository interface for queries.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IReadRepository<TEntity> where TEntity : class
{
    /// <summary>
    /// Gets a queryable for the entity.
    /// </summary>
    /// <returns>An IQueryable for the entity type.</returns>
    IQueryable<TEntity> Gets();

    /// <summary>
    /// Gets a queryable projected to a DTO type.
    /// </summary>
    /// <typeparam name="TDto">The DTO type.</typeparam>
    /// <returns>An IQueryable for the DTO type.</returns>
    IQueryable<TDto> GetDto<TDto>() where TDto : class;

    /// <summary>
    /// Gets an entity by its identifier.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entity if found; otherwise, null.</returns>
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
```

#### Repository Implementation

```csharp
namespace DKNet.EfCore.Repos;

/// <summary>
/// Base repository implementation.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class Repository<TEntity> : IRepository<TEntity>, IReadRepository<TEntity>
    where TEntity : class
{
    protected readonly DbContext Context;
    protected readonly DbSet<TEntity> DbSet;

    public Repository(DbContext context)
    {
        Context = context;
        DbSet = context.Set<TEntity>();
    }

    public virtual IQueryable<TEntity> Gets() => DbSet;

    public virtual IQueryable<TDto> GetDto<TDto>() where TDto : class
    {
        return Gets().ProjectToType<TDto>();
    }

    public virtual async Task<TEntity?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FindAsync(new object[] { id }, cancellationToken);
    }

    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await FindAsync(id, cancellationToken);
    }

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var entry = await DbSet.AddAsync(entity, cancellationToken);
        return entry.Entity;
    }

    public virtual void Update(TEntity entity)
    {
        DbSet.Update(entity);
    }

    public virtual void Delete(TEntity entity)
    {
        DbSet.Remove(entity);
    }

    public virtual async Task<IEnumerable<TEntity>> FindAsync(
        Specification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await Gets()
            .Where(specification.ToExpression())
            .ToListAsync(cancellationToken);
    }
}
```

### DKNet.EfCore.Specifications

#### Specification Pattern

```csharp
namespace DKNet.EfCore.Specifications;

/// <summary>
/// Base class for specifications.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public abstract class Specification<T>
{
    /// <summary>
    /// Converts the specification to a LINQ expression.
    /// </summary>
    /// <returns>A LINQ expression representing the specification.</returns>
    public abstract Expression<Func<T, bool>> ToExpression();

    /// <summary>
    /// Combines this specification with another using AND logic.
    /// </summary>
    /// <param name="specification">The specification to combine.</param>
    /// <returns>A new specification representing the AND combination.</returns>
    public Specification<T> And(Specification<T> specification)
    {
        return new AndSpecification<T>(this, specification);
    }

    /// <summary>
    /// Combines this specification with another using OR logic.
    /// </summary>
    /// <param name="specification">The specification to combine.</param>
    /// <returns>A new specification representing the OR combination.</returns>
    public Specification<T> Or(Specification<T> specification)
    {
        return new OrSpecification<T>(this, specification);
    }

    /// <summary>
    /// Creates a specification that negates this specification.
    /// </summary>
    /// <returns>A new specification representing the negation.</returns>
    public Specification<T> Not()
    {
        return new NotSpecification<T>(this);
    }

    /// <summary>
    /// Implicit conversion from specification to expression.
    /// </summary>
    /// <param name="specification">The specification.</param>
    public static implicit operator Expression<Func<T, bool>>(Specification<T> specification)
    {
        return specification.ToExpression();
    }
}
```

---

## 📨 Messaging & CQRS

### DKNet.SlimBus.Extensions

#### Command and Query Interfaces

```csharp
namespace DKNet.SlimBus.Extensions;

/// <summary>
/// Base interface for commands.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Gets or sets the user who initiated the command.
    /// </summary>
    string? ByUser { get; set; }
}

/// <summary>
/// Interface for commands that return a response.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface ICommand<TResponse> : ICommand
{
}

/// <summary>
/// Interface for queries that return a response.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IQuery<TResponse>
{
}

/// <summary>
/// Interface for command handlers.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task<IResultBase> OnHandle(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for command handlers with response.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    Task<IResult<TResponse>> OnHandle(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for query handlers.
/// </summary>
/// <typeparam name="TQuery">The query type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<TResponse> OnHandle(TQuery query, CancellationToken cancellationToken = default);
}
```

#### Result Pattern

```csharp
/// <summary>
/// Base interface for operation results.
/// </summary>
public interface IResultBase
{
    bool IsSuccess { get; }
    bool IsFailure { get; }
    string ErrorMessage { get; }
    IEnumerable<Error> Errors { get; }
}

/// <summary>
/// Interface for operation results with value.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public interface IResult<T> : IResultBase
{
    T Value { get; }
}

/// <summary>
/// Represents an operation result.
/// </summary>
public class Result : IResultBase
{
    protected Result(bool isSuccess, string errorMessage, IEnumerable<Error>? errors = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Errors = errors ?? Enumerable.Empty<Error>();
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string ErrorMessage { get; }
    public IEnumerable<Error> Errors { get; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>A successful result.</returns>
    public static Result Ok() => new(true, string.Empty);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A failed result.</returns>
    public static Result Fail(string errorMessage) => new(false, errorMessage);

    /// <summary>
    /// Creates a successful result with value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>A successful result with value.</returns>
    public static Result<T> Ok<T>(T value) => new(true, value, string.Empty);

    /// <summary>
    /// Creates a failed result with value type.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A failed result.</returns>
    public static Result<T> Fail<T>(string errorMessage) => new(false, default!, errorMessage);
}
```

---

## 🗃️ Blob Storage Services

### DKNet.Svc.BlobStorage.Abstractions

#### Core Interfaces

```csharp
namespace DKNet.Svc.BlobStorage.Abstractions;

/// <summary>
/// Interface for blob storage operations.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Uploads a blob to storage.
    /// </summary>
    /// <param name="blobName">The blob name/path.</param>
    /// <param name="stream">The stream containing the blob data.</param>
    /// <param name="contentType">The content type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The blob URI.</returns>
    Task<string> UploadAsync(
        string blobName,
        Stream stream,
        string? contentType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a blob from storage.
    /// </summary>
    /// <param name="blobName">The blob name/path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A stream containing the blob data.</returns>
    Task<Stream> DownloadAsync(
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a blob from storage.
    /// </summary>
    /// <param name="blobName">The blob name/path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task DeleteAsync(
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a blob exists in storage.
    /// </summary>
    /// <param name="blobName">The blob name/path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>true if the blob exists; otherwise, false.</returns>
    Task<bool> ExistsAsync(
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists blobs with optional prefix filter.
    /// </summary>
    /// <param name="prefix">The prefix filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of blob information.</returns>
    Task<IEnumerable<BlobInfo>> ListAsync(
        string? prefix = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Information about a blob.
/// </summary>
public record BlobInfo
{
    public required string Name { get; init; }
    public required long Size { get; init; }
    public required DateTime LastModified { get; init; }
    public string? ContentType { get; init; }
    public string? ETag { get; init; }
}
```

---

## 🔐 Security & Encryption

### DKNet.Svc.Encryption

#### Authenticated Encryption

```csharp
namespace DKNet.Svc.Encryption;

public interface IAesGcmEncryption : IDisposable
{
    string Key { get; }

    string EncryptString(string plainText, byte[]? associatedData = null);
    string DecryptString(string cipherPackage, byte[]? associatedData = null);

    string Encrypt(string plainText, string base64Key, byte[]? associatedData = null);
    string Decrypt(string cipherPackage, string base64Key, byte[]? associatedData = null);
}
```

#### Password-Based Encryption

```csharp
public interface IPasswordAesEncryption
{
    string Encrypt(string plainText, string password);
    string Decrypt(string cipherText, string password);
}
```

#### RSA Encryption & Signatures

```csharp
public interface IRsaEncryption : IDisposable
{
    string PublicKey { get; }
    string? PrivateKey { get; }

    string Encrypt(string plainText);
    string Decrypt(string cipherText);

    string Sign(string plainText);
    bool Verify(string plainText, string signature);
}
```

#### Hashing & HMAC

```csharp
public interface IHmacHashing : IDisposable
{
    string Compute(string content, string secret, HmacAlgorithm algorithm = HmacAlgorithm.Sha256, bool asBase64 = true);
    bool Verify(string content, string secret, string signature, HmacAlgorithm algorithm = HmacAlgorithm.Sha256);
}

public interface IShaHashing : IDisposable
{
    string ComputeHash(string content, HashAlgorithmKind algorithm = HashAlgorithmKind.Sha256, bool upperCase = true);
    bool VerifyHash(string content, string expectedHash, HashAlgorithmKind algorithm = HashAlgorithmKind.Sha256);
}
```

---

## 📝 PDF Generation

### DKNet.Svc.PdfGenerators

#### Core Facade

```csharp
namespace DKNet.Svc.PdfGenerators;

public class PdfGenerator
{
    public PdfGenerator(PdfGeneratorOptions options, ILogger<PdfGenerator>? logger = null);

    public Task GenerateFromMarkdownAsync(string markdownPath, string outputPdfPath, CancellationToken cancellationToken = default);
    public Task GenerateFromHtmlAsync(string html, string outputPdfPath, CancellationToken cancellationToken = default);
    public Task GenerateFromTemplateAsync<TModel>(TModel model, string outputPdfPath, CancellationToken cancellationToken = default);
}
```

#### Options Snapshot

```csharp
public sealed class PdfGeneratorOptions
{
    public string? Title { get; set; }
    public PdfTheme Theme { get; set; } = PdfTheme.Default;
    public TableOfContentsOptions TableOfContents { get; set; } = new();
    public HeaderFooterOptions Header { get; set; } = new();
    public HeaderFooterOptions Footer { get; set; } = new();
    public ModuleInformation Module { get; set; } = new();
    public List<ResourceDefinition> AdditionalResources { get; } = new();
}
```

---

## 🌐 ASP.NET Core Utilities

### DKNet.AspCore.Tasks

#### Background Task Contract

```csharp
namespace DKNet.AspCore.Tasks;

public interface IBackgroundTask
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
```

#### Registration Extensions

```csharp
namespace Microsoft.Extensions.DependencyInjection;

public static class TaskSetups
{
    public static IServiceCollection AddBackgroundJob<TTask>(this IServiceCollection services)
        where TTask : class, IBackgroundTask;

    public static IServiceCollection AddBackgroundJobFrom(this IServiceCollection services, Assembly[] assemblies);
}
```

---

## ☁️ Aspire Integrations

### Aspire.Hosting.ServiceBus

```csharp
namespace Aspire.Hosting.ServiceBus;

public sealed class ServiceBusResource : IResourceWithConnectionString
{
    public string Name { get; }

    public ServiceBusResource WithNamespace(string namespaceName);
    public ServiceBusResource WithQueue(string queueName, Action<ServiceBusQueueOptions>? configure = null);
    public ServiceBusResource WithTopic(string topicName, Action<ServiceBusTopicOptions>? configure = null);
    public ServiceBusResource WithSecretsFrom(string parameterName);
}

public static class ServiceBusExtensions
{
    public static ServiceBusResource AddServiceBus(this IDistributedApplicationBuilder builder, string name, Action<ServiceBusResource>? configure = null);
}
```

---

## ⚙️ Configuration Options

### DKNet Configuration

```csharp
namespace DKNet.Configuration;

/// <summary>
/// Configuration options for DKNet Framework.
/// </summary>
public class DKNetOptions
{
    /// <summary>
    /// Gets or sets whether audit fields are automatically populated.
    /// </summary>
    public bool EnableAuditFields { get; set; } = true;

    /// <summary>
    /// Gets or sets whether soft delete is enabled.
    /// </summary>
    public bool EnableSoftDelete { get; set; } = true;

    /// <summary>
    /// Gets or sets the default timezone for date operations.
    /// </summary>
    public string DefaultTimeZone { get; set; } = "UTC";

    /// <summary>
    /// Gets or sets the maximum page size for paged queries.
    /// </summary>
    public int MaxPageSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether detailed error information is included in responses.
    /// </summary>
    public bool EnableDetailedErrors { get; set; } = false;

    /// <summary>
    /// Gets or sets whether sensitive data logging is enabled.
    /// </summary>
    public bool EnableSensitiveDataLogging { get; set; } = false;
}
```

### Service Registration Extensions

```csharp
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds DKNet core services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration action.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDKNet(
        this IServiceCollection services,
        Action<DKNetOptions>? configuration = null);

    /// <summary>
    /// Adds DKNet repositories for the specified DbContext.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDKNetRepositories<TContext>(
        this IServiceCollection services)
        where TContext : DbContext;

    /// <summary>
    /// Adds DKNet blob storage services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration action.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDKNetBlobStorage(
        this IServiceCollection services,
        Action<BlobStorageBuilder> configuration);

    /// <summary>
    /// Adds DKNet SlimBus integration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDKNetSlimBus(this IServiceCollection services);
}
```

---

## 📖 Usage Examples

For practical usage examples of all APIs, see:
- **[Examples & Recipes](Examples/README.md)** - Comprehensive usage examples
- **[SlimBus Template](../src/Templates/SlimBus.ApiEndpoints/)** - Complete reference implementation
- **[Unit Tests](../src/Tests/)** - API usage in tests

---

> 📝 **API Documentation**: This reference covers the most commonly used APIs. For complete API documentation, see the XML documentation in the source code or generated documentation.