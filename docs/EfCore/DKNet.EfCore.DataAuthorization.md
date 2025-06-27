# DKNet.EfCore.DataAuthorization

**Data authorization and access control mechanisms for Entity Framework Core that provide row-level security, role-based access control, and policy-based authorization to ensure users can only access data they are authorized to see, supporting Domain-Driven Design (DDD) and Onion Architecture principles.**

## What is this project?

DKNet.EfCore.DataAuthorization provides a comprehensive data authorization framework for Entity Framework Core applications. It implements row-level security patterns that automatically filter data based on user permissions, roles, and custom authorization policies, ensuring that users can only access data they are authorized to view or modify.

### Key Features

- **IDataOwnerProvider**: Interface for defining data ownership and access rules
- **IDataOwnerDbContext**: DbContext extension for automatic authorization filtering
- **IOwnedBy Interface**: Contract for entities that have ownership or access control
- **Policy-Based Authorization**: Flexible authorization rules based on custom policies
- **Role-Based Access Control**: Traditional RBAC implementation with EF Core integration
- **Multi-Tenancy Support**: Built-in support for multi-tenant data isolation
- **Query Filtering**: Automatic query filtering based on authorization rules
- **Dynamic Authorization**: Runtime authorization rule evaluation
- **Audit Integration**: Comprehensive audit trails for authorization decisions

## How it contributes to DDD and Onion Architecture

### Security Layer Implementation

DKNet.EfCore.DataAuthorization implements **Security and Authorization concerns** that span multiple layers of the Onion Architecture, providing data access control without compromising domain logic:

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 Presentation Layer                        │
│                   (Controllers, API Endpoints)                  │
│                                                                 │
│  Benefits from: Automatic data filtering, authorization checks │
│  Provides: User context, role information                      │
└─────────────────────────────┬───────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🎯 Application Layer                          │
│              (Use Cases, Application Services)                  │
│                                                                 │
│  Benefits from: Pre-filtered data, authorization validation    │
│  Provides: Business context for authorization decisions        │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                             │
│           (Entities, Aggregates, Domain Services)              │
│                                                                 │
│  📋 IOwnedBy - Ownership contracts                             │
│  🎭 Authorization policies expressed in business terms         │
│  🏷️ Domain entities unaware of authorization implementation    │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🗄️ Infrastructure Layer                        │
│                  (Authorization, Data Access)                  │
│                                                                 │
│  🔒 IDataOwnerProvider - Authorization rule implementation     │
│  🗃️ IDataOwnerDbContext - Automatic query filtering           │
│  📊 Authorization policies and rule engines                    │
│  🔍 Query interceptors for access control                      │
│  📝 Audit logging for authorization decisions                  │
└─────────────────────────────────────────────────────────────────┘
```

### DDD Benefits

1. **Domain Security**: Authorization rules expressed in business terms
2. **Aggregate Protection**: Ensure aggregate consistency and access control
3. **Business Policy Enforcement**: Authorization aligned with business rules
4. **Multi-Tenant Support**: Clean separation of tenant data
5. **Audit Trails**: Comprehensive business event tracking for compliance
6. **Context Preservation**: User context maintained throughout domain operations

### Onion Architecture Benefits

1. **Dependency Inversion**: Domain defines authorization contracts, infrastructure implements them
2. **Separation of Concerns**: Authorization logic separated from business logic
3. **Testability**: Authorization can be mocked for unit testing
4. **Technology Independence**: Authorization abstractions can work with any data access technology
5. **Maintainability**: Centralized authorization logic with clear boundaries
6. **Compliance Ready**: Built-in support for regulatory compliance requirements

## How to use it

### Installation

```bash
dotnet add package DKNet.EfCore.DataAuthorization
dotnet add package DKNet.EfCore.Abstractions
```

### Basic Usage Examples

#### 1. Implementing Data Ownership

```csharp
using DKNet.EfCore.DataAuthorization;
using DKNet.EfCore.Abstractions;

// Entity that implements ownership
public class Document : Entity<int>, IOwnedBy<string>
{
    public string OwnerId { get; set; } // User ID who owns this document
    public string Title { get; set; }
    public string Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsPublic { get; set; }
    
    // Additional authorization properties
    public List<string> SharedWith { get; set; } = new();
    public string DepartmentId { get; set; }
    
    public Document(string title, string content, string ownerId)
    {
        Title = title;
        Content = content;
        OwnerId = ownerId;
        CreatedAt = DateTime.UtcNow;
    }
}

// Multi-tenant entity
public class Order : Entity<int>, IOwnedBy<string>
{
    public string OwnerId { get; set; } // Tenant ID
    public int CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
    public OrderStatus Status { get; set; }
    
    public Order(int customerId, decimal totalAmount, string tenantId)
    {
        CustomerId = customerId;
        TotalAmount = totalAmount;
        OwnerId = tenantId;
        OrderDate = DateTime.UtcNow;
        Status = OrderStatus.Pending;
    }
}
```

#### 2. Data Owner Provider Implementation

```csharp
public class DocumentDataOwnerProvider : IDataOwnerProvider
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRoleService _userRoleService;
    private readonly ILogger<DocumentDataOwnerProvider> _logger;
    
    public DocumentDataOwnerProvider(
        ICurrentUserService currentUserService,
        IUserRoleService userRoleService,
        ILogger<DocumentDataOwnerProvider> logger)
    {
        _currentUserService = currentUserService;
        _userRoleService = userRoleService;
        _logger = logger;
    }
    
    public async Task<bool> CanAccessAsync<TEntity>(TEntity entity, string operation, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (currentUser == null)
        {
            _logger.LogWarning("No current user found for authorization check");
            return false;
        }
        
        return entity switch
        {
            Document document => await CanAccessDocumentAsync(document, currentUser, operation, cancellationToken),
            Order order => await CanAccessOrderAsync(order, currentUser, operation, cancellationToken),
            _ => await DefaultAuthorizationAsync(entity, currentUser, operation, cancellationToken)
        };
    }
    
    private async Task<bool> CanAccessDocumentAsync(Document document, User currentUser, string operation, CancellationToken cancellationToken)
    {
        // Owner can do everything
        if (document.OwnerId == currentUser.Id)
        {
            return true;
        }
        
        // Public documents can be read by anyone authenticated
        if (document.IsPublic && operation == "Read")
        {
            return true;
        }
        
        // Check if document is shared with current user
        if (document.SharedWith.Contains(currentUser.Id) && operation == "Read")
        {
            return true;
        }
        
        // Check department access
        if (document.DepartmentId == currentUser.DepartmentId)
        {
            var departmentRoles = await _userRoleService.GetDepartmentRolesAsync(currentUser.Id, currentUser.DepartmentId);
            
            return operation switch
            {
                "Read" => departmentRoles.Contains("Viewer") || departmentRoles.Contains("Editor") || departmentRoles.Contains("Admin"),
                "Update" => departmentRoles.Contains("Editor") || departmentRoles.Contains("Admin"),
                "Delete" => departmentRoles.Contains("Admin"),
                _ => false
            };
        }
        
        // Admin users can access everything
        var userRoles = await _userRoleService.GetUserRolesAsync(currentUser.Id);
        if (userRoles.Contains("SystemAdmin"))
        {
            return true;
        }
        
        _logger.LogWarning("User {UserId} denied access to document {DocumentId} for operation {Operation}",
            currentUser.Id, document.Id, operation);
        
        return false;
    }
    
    private async Task<bool> CanAccessOrderAsync(Order order, User currentUser, string operation, CancellationToken cancellationToken)
    {
        // Multi-tenant check - user must belong to the same tenant
        if (order.OwnerId != currentUser.TenantId)
        {
            return false;
        }
        
        // Check user permissions within tenant
        var userRoles = await _userRoleService.GetUserRolesAsync(currentUser.Id);
        
        return operation switch
        {
            "Read" => userRoles.Contains("OrderViewer") || userRoles.Contains("OrderManager") || userRoles.Contains("TenantAdmin"),
            "Update" => userRoles.Contains("OrderManager") || userRoles.Contains("TenantAdmin"),
            "Delete" => userRoles.Contains("TenantAdmin"),
            _ => false
        };
    }
    
    public IQueryable<TEntity> ApplyAuthorizationFilter<TEntity>(IQueryable<TEntity> query) 
        where TEntity : class
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (currentUser == null)
        {
            return query.Where(_ => false); // No user, no access
        }
        
        return typeof(TEntity).Name switch
        {
            nameof(Document) => ApplyDocumentFilter(query.Cast<Document>(), currentUser).Cast<TEntity>(),
            nameof(Order) => ApplyOrderFilter(query.Cast<Order>(), currentUser).Cast<TEntity>(),
            _ => query // No filtering for entities without authorization
        };
    }
    
    private IQueryable<Document> ApplyDocumentFilter(IQueryable<Document> query, User currentUser)
    {
        return query.Where(d => 
            d.OwnerId == currentUser.Id ||                           // Owner access
            d.IsPublic ||                                            // Public documents
            d.SharedWith.Contains(currentUser.Id) ||                 // Shared documents
            d.DepartmentId == currentUser.DepartmentId ||            // Department access
            currentUser.Roles.Contains("SystemAdmin"));              // Admin access
    }
    
    private IQueryable<Order> ApplyOrderFilter(IQueryable<Order> query, User currentUser)
    {
        return query.Where(o => o.OwnerId == currentUser.TenantId); // Tenant isolation
    }
}
```

#### 3. DbContext with Authorization

```csharp
public class AuthorizedDbContext : DbContext, IDataOwnerDbContext
{
    private readonly IDataOwnerProvider _dataOwnerProvider;
    
    public AuthorizedDbContext(
        DbContextOptions<AuthorizedDbContext> options,
        IDataOwnerProvider dataOwnerProvider) : base(options)
    {
        _dataOwnerProvider = dataOwnerProvider;
    }
    
    public DbSet<Document> Documents { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<User> Users { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply global query filters for authorization
        modelBuilder.Entity<Document>()
            .HasQueryFilter(d => ApplyDocumentAuthorizationFilter(d));
        
        modelBuilder.Entity<Order>()
            .HasQueryFilter(o => ApplyOrderAuthorizationFilter(o));
    }
    
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Validate authorization before saving
        await ValidateAuthorizationAsync(cancellationToken);
        
        return await base.SaveChangesAsync(cancellationToken);
    }
    
    private async Task ValidateAuthorizationAsync(CancellationToken cancellationToken)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is IOwnedBy<string>)
            {
                var operation = entry.State switch
                {
                    EntityState.Added => "Create",
                    EntityState.Modified => "Update",
                    EntityState.Deleted => "Delete",
                    _ => null
                };
                
                if (operation != null)
                {
                    var canAccess = await _dataOwnerProvider.CanAccessAsync(entry.Entity, operation, cancellationToken);
                    if (!canAccess)
                    {
                        throw new UnauthorizedAccessException(
                            $"User is not authorized to {operation.ToLower()} this {entry.Entity.GetType().Name}");
                    }
                }
            }
        }
    }
    
    private bool ApplyDocumentAuthorizationFilter(Document document)
    {
        // This will be translated to SQL by EF Core
        // Implementation depends on your authorization logic
        return true; // Simplified for example
    }
    
    private bool ApplyOrderAuthorizationFilter(Order order)
    {
        // Multi-tenant filtering
        return true; // Simplified for example
    }
    
    public IQueryable<TEntity> AuthorizedSet<TEntity>() where TEntity : class
    {
        var baseQuery = Set<TEntity>();
        return _dataOwnerProvider.ApplyAuthorizationFilter(baseQuery);
    }
}
```

#### 4. Service Layer Integration

```csharp
public class DocumentService
{
    private readonly AuthorizedDbContext _context;
    private readonly IDataOwnerProvider _dataOwnerProvider;
    private readonly ICurrentUserService _currentUserService;
    
    public DocumentService(
        AuthorizedDbContext context,
        IDataOwnerProvider dataOwnerProvider,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _dataOwnerProvider = dataOwnerProvider;
        _currentUserService = currentUserService;
    }
    
    // Automatically filtered by authorization
    public async Task<IEnumerable<Document>> GetDocumentsAsync()
    {
        return await _context.AuthorizedSet<Document>()
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }
    
    public async Task<Document?> GetDocumentAsync(int documentId)
    {
        return await _context.AuthorizedSet<Document>()
            .FirstOrDefaultAsync(d => d.Id == documentId);
    }
    
    public async Task<Document> CreateDocumentAsync(CreateDocumentRequest request)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (currentUser == null)
            throw new UnauthorizedAccessException("No current user");
        
        var document = new Document(request.Title, request.Content, currentUser.Id)
        {
            IsPublic = request.IsPublic,
            DepartmentId = currentUser.DepartmentId
        };
        
        // Authorization check is performed in SaveChangesAsync
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();
        
        return document;
    }
    
    public async Task<Document> UpdateDocumentAsync(int documentId, UpdateDocumentRequest request)
    {
        // This will only return the document if user is authorized to see it
        var document = await _context.AuthorizedSet<Document>()
            .FirstOrDefaultAsync(d => d.Id == documentId);
        
        if (document == null)
            throw new EntityNotFoundException($"Document {documentId} not found or access denied");
        
        document.Title = request.Title;
        document.Content = request.Content;
        document.IsPublic = request.IsPublic;
        
        // Authorization for update is checked in SaveChangesAsync
        await _context.SaveChangesAsync();
        
        return document;
    }
    
    public async Task ShareDocumentAsync(int documentId, ShareDocumentRequest request)
    {
        var document = await _context.AuthorizedSet<Document>()
            .FirstOrDefaultAsync(d => d.Id == documentId);
        
        if (document == null)
            throw new EntityNotFoundException($"Document {documentId} not found or access denied");
        
        // Only owner can share documents
        var currentUser = _currentUserService.GetCurrentUser();
        if (document.OwnerId != currentUser?.Id)
            throw new UnauthorizedAccessException("Only document owner can share documents");
        
        document.SharedWith.AddRange(request.UserIds.Except(document.SharedWith));
        await _context.SaveChangesAsync();
    }
}
```

#### 5. Service Registration

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataAuthorization(this IServiceCollection services)
    {
        // Register authorization services
        services.AddScoped<IDataOwnerProvider, DocumentDataOwnerProvider>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IUserRoleService, UserRoleService>();
        
        return services;
    }
}

// In Program.cs or Startup.cs
services.AddDbContext<AuthorizedDbContext>(options =>
    options.UseSqlServer(connectionString));

services.AddDataAuthorization();
```

### Advanced Usage Examples

#### 1. Policy-Based Authorization

```csharp
public class PolicyBasedDataOwnerProvider : IDataOwnerProvider
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentUserService _currentUserService;
    
    public async Task<bool> CanAccessAsync<TEntity>(TEntity entity, string operation, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var authorizationResult = await _authorizationService.AuthorizeAsync(
            currentUser,
            entity,
            $"{typeof(TEntity).Name}.{operation}");
        
        return authorizationResult.Succeeded;
    }
    
    public IQueryable<TEntity> ApplyAuthorizationFilter<TEntity>(IQueryable<TEntity> query) 
        where TEntity : class
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (currentUser == null) return query.Where(_ => false);
        
        // Apply policy-based filtering
        return ApplyPolicyFilter(query, currentUser);
    }
    
    private IQueryable<TEntity> ApplyPolicyFilter<TEntity>(IQueryable<TEntity> query, User currentUser) 
        where TEntity : class
    {
        // Implementation depends on your policy framework
        // This could integrate with ASP.NET Core Authorization Policies
        return query;
    }
}
```

#### 2. Hierarchical Authorization

```csharp
public class HierarchicalDataOwnerProvider : IDataOwnerProvider
{
    private readonly IOrganizationService _organizationService;
    private readonly ICurrentUserService _currentUserService;
    
    public async Task<bool> CanAccessAsync<TEntity>(TEntity entity, string operation, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        if (entity is not IHierarchicalEntity hierarchicalEntity)
            return true; // No restrictions for non-hierarchical entities
        
        var currentUser = _currentUserService.GetCurrentUser();
        if (currentUser == null) return false;
        
        // Check if user has access to this level of the hierarchy
        var userAccessLevels = await _organizationService.GetUserAccessLevelsAsync(currentUser.Id);
        
        return userAccessLevels.Any(level => 
            hierarchicalEntity.OrganizationPath.StartsWith(level.Path) &&
            level.Permissions.Contains(operation));
    }
    
    public IQueryable<TEntity> ApplyAuthorizationFilter<TEntity>(IQueryable<TEntity> query) 
        where TEntity : class
    {
        if (typeof(IHierarchicalEntity).IsAssignableFrom(typeof(TEntity)))
        {
            return ApplyHierarchicalFilter(query);
        }
        
        return query;
    }
    
    private IQueryable<TEntity> ApplyHierarchicalFilter<TEntity>(IQueryable<TEntity> query) 
        where TEntity : class
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (currentUser == null)
            return query.Where(_ => false);
        
        // Filter based on organizational hierarchy
        // This would be translated to appropriate SQL
        return query;
    }
}
```

#### 3. Field-Level Authorization

```csharp
public class FieldLevelAuthorizationService
{
    private readonly IDataOwnerProvider _dataOwnerProvider;
    private readonly ICurrentUserService _currentUserService;
    
    public async Task<TDto> ApplyFieldLevelAuthorizationAsync<TEntity, TDto>(TEntity entity, TDto dto)
        where TEntity : class
        where TDto : class
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (currentUser == null) return dto;
        
        var sensitiveFields = typeof(TDto).GetProperties()
            .Where(p => p.GetCustomAttribute<SensitiveDataAttribute>() != null)
            .ToList();
        
        foreach (var field in sensitiveFields)
        {
            var canAccessField = await _dataOwnerProvider.CanAccessAsync(entity, $"Read.{field.Name}");
            if (!canAccessField)
            {
                // Clear sensitive field value
                field.SetValue(dto, GetDefaultValue(field.PropertyType));
            }
        }
        
        return dto;
    }
    
    private static object? GetDefaultValue(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
```

## Best Practices

### 1. Authorization Rule Design

```csharp
// Good: Clear, business-focused authorization rules
public async Task<bool> CanAccessDocumentAsync(Document document, User user, string operation)
{
    return operation switch
    {
        "Read" => user.Id == document.OwnerId || 
                 document.IsPublic || 
                 document.SharedWith.Contains(user.Id),
        "Update" => user.Id == document.OwnerId,
        "Delete" => user.Id == document.OwnerId && user.HasRole("DocumentAdmin"),
        _ => false
    };
}

// Avoid: Complex authorization logic mixed with data access
public async Task<Document> GetDocumentAsync(int id)
{
    var document = await _context.Documents.FindAsync(id);
    // DON'T: Mix authorization with data retrieval
    if (document.OwnerId != _currentUser.Id && !document.IsPublic && ...)
        throw new UnauthorizedAccessException();
    return document;
}
```

### 2. Performance Considerations

```csharp
// Good: Apply filters at database level
public IQueryable<Document> GetAuthorizedDocuments()
{
    return _context.Documents
        .Where(d => d.OwnerId == _currentUser.Id || d.IsPublic);
}

// Avoid: Filtering in memory
public async Task<IEnumerable<Document>> GetAuthorizedDocuments()
{
    var allDocuments = await _context.Documents.ToListAsync();
    return allDocuments.Where(d => CanAccess(d, "Read")); // Memory filtering
}
```

### 3. Testing Authorization

```csharp
[Test]
public async Task GetDocuments_UserCanOnlyAccessOwnDocuments()
{
    // Arrange
    var user1 = new User { Id = "user1" };
    var user2 = new User { Id = "user2" };
    
    var doc1 = new Document("Doc 1", "Content 1", user1.Id);
    var doc2 = new Document("Doc 2", "Content 2", user2.Id);
    
    var context = CreateContextWithUser(user1);
    context.Documents.AddRange(doc1, doc2);
    await context.SaveChangesAsync();
    
    // Act
    var results = await context.AuthorizedSet<Document>().ToListAsync();
    
    // Assert
    Assert.Single(results);
    Assert.Equal(doc1.Id, results.First().Id);
}
```

## Integration with Other DKNet Components

DKNet.EfCore.DataAuthorization integrates seamlessly with other DKNet components:

- **DKNet.EfCore.Abstractions**: Uses entity interfaces and base classes
- **DKNet.EfCore.Hooks**: Integrates with authorization hooks
- **DKNet.EfCore.Repos**: Provides authorized repository implementations
- **DKNet.EfCore.Events**: Supports authorization-related domain events
- **DKNet.Fw.Extensions**: Leverages core framework utilities

---

> 💡 **Security Tip**: Use DKNet.EfCore.DataAuthorization to implement defense-in-depth security for your data access layer. Always apply authorization at the database query level to prevent data leakage, and combine with application-level authorization for comprehensive security. Regularly audit your authorization rules and test them thoroughly to ensure they work as expected.