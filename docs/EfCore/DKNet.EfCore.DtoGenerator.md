# DKNet.EfCore.DtoGenerator

**A compile-time source generator that automatically creates immutable DTO (Data Transfer Object) types from Entity Framework Core entities or any POCO classes, eliminating boilerplate code while maintaining type safety.**

## What is this project?

DKNet.EfCore.DtoGenerator is a Roslyn Incremental Source Generator that generates DTO classes at compile time. Instead of manually creating and maintaining DTO classes that mirror your entities, you simply apply the `[GenerateDto]` attribute to an empty partial record or class, and the generator creates all the properties and mapping methods automatically.

The generator intelligently synthesizes `public init` properties for every public instance readable property on the source entity, while excluding indexers and static properties. It also generates helpful mapping methods (`FromEntity`, `ToEntity`, and `FromEntities`) that leverage Mapster when available or fall back to property-by-property initialization.

### Key Features

- **Compile-Time Code Generation**: No runtime reflection or performance overhead
- **Type-Safe DTOs**: Compile-time errors prevent mismatches between entities and DTOs
- **Automatic Property Mapping**: Generates all properties from source entity automatically
- **Mapster Integration**: Leverages Mapster for efficient mapping when available
- **Fallback Mapping**: Provides property-by-property initialization when Mapster is not present
- **Partial Class Support**: Allows customization and extension of generated DTOs
- **Property Exclusion**: Exclude specific properties using the `Exclude` parameter
- **Incremental Generation**: Efficient generation that only regenerates when needed
- **Zero Runtime Dependencies**: Generated code has no runtime dependencies on the generator

## How it contributes to DDD and Onion Architecture

### Application Layer DTO Generation

DKNet.EfCore.DtoGenerator primarily serves the **Application Layer** and **Presentation Layer** of the Onion Architecture by providing clean, immutable DTOs that decouple external representations from internal domain models:

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 Presentation Layer                        │
│                   (Controllers, API Endpoints)                  │
│                                                                 │
│  Uses: Generated DTOs for request/response models              │
│  📄 CustomerDto, OrderDto, ProductDto                          │
│  ✅ Validation happens on DTOs, not domain entities            │
│  🔒 Domain entities never exposed directly to clients          │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🎯 Application Layer                          │
│              (Use Cases, Application Services)                  │
│                                                                 │
│  🎯 DKNet.EfCore.DtoGenerator - Generates DTOs at compile time │
│  📋 Maps between domain entities and DTOs                      │
│  🔄 Uses Mapster for efficient transformations                 │
│  ✅ Maintains separation between domain and presentation       │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                             │
│           (Entities, Aggregates, Domain Services)              │
│                                                                 │
│  Domain entities remain pure and focused on business logic     │
│  No knowledge of DTOs or external representations              │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🗄️ Infrastructure Layer                        │
│                  (Data Access, Persistence)                    │
│                                                                 │
│  Domain entities persist without DTO concerns                  │
└─────────────────────────────────────────────────────────────────┘
```

### DDD Benefits

#### 1. Anti-Corruption Layer
DTOs serve as an anti-corruption layer, preventing external API concerns from leaking into the domain model:

```csharp
// Domain Entity - Pure business logic
public class Customer : AggregateRoot
{
    public string Name { get; private set; }
    public Email Email { get; private set; }
    public CustomerStatus Status { get; private set; }
    
    public void ActivateAccount() 
    {
        // Business logic
    }
}

// Generated DTO - External representation
[GenerateDto(typeof(Customer))]
public partial record CustomerDto;
// Auto-generated properties: Name, Email, Status (as string/primitive types)
```

#### 2. Bounded Context Boundaries
DTOs help maintain clear boundaries between bounded contexts by providing explicit translation points:

```csharp
// Order Context
[GenerateDto(typeof(Order))]
public partial record OrderDto;

// Customer Context
[GenerateDto(typeof(Customer))]
public partial record CustomerSummaryDto;

// Integration between contexts uses DTOs, not domain entities
```

#### 3. Aggregate Protection
DTOs prevent clients from directly modifying aggregate internals:

```csharp
// Domain aggregate with encapsulated behavior
public class Order : AggregateRoot
{
    private List<OrderItem> _items = new();
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
    
    public void AddItem(Product product, int quantity)
    {
        // Business rules enforced here
    }
}

// DTO for reading - no behavior, just data
[GenerateDto(typeof(Order))]
public partial record OrderDto;
```

### Onion Architecture Benefits

#### 1. Layer Decoupling
Generated DTOs enable complete decoupling between presentation and domain layers:

```csharp
// Application Service - translates between layers
public class CustomerService
{
    private readonly ICustomerRepository _repository;
    
    public async Task<CustomerDto> GetCustomerAsync(Guid id)
    {
        var customer = await _repository.GetByIdAsync(id);
        return CustomerDto.FromEntity(customer); // Generated mapping
    }
    
    public async Task<Guid> CreateCustomerAsync(CreateCustomerDto dto)
    {
        var customer = dto.ToEntity(); // Generated mapping
        await _repository.AddAsync(customer);
        return customer.Id;
    }
}
```

#### 2. Testability
DTOs with generated mapping methods are easily testable:

```csharp
[Fact]
public void CustomerDto_ShouldMapFromEntity()
{
    // Arrange
    var customer = new Customer("John Doe", "john@example.com");
    
    // Act
    var dto = CustomerDto.FromEntity(customer);
    
    // Assert
    dto.Name.ShouldBe("John Doe");
    dto.Email.ShouldBe("john@example.com");
}
```

#### 3. API Versioning Support
Different DTO versions can be generated from the same entity:

```csharp
// V1 API
[GenerateDto(typeof(Customer))]
public partial record CustomerDtoV1;

// V2 API - exclude sensitive fields
[GenerateDto(typeof(Customer), Exclude = new[] { "InternalNotes", "CreditScore" })]
public partial record CustomerDtoV2;
```

## Installation and Setup

### NuGet Package Installation

```xml
<ItemGroup>
  <PackageReference Include="DKNet.EfCore.DtoGenerator" Version="1.0.0" 
                    PrivateAssets="all" OutputItemType="Analyzer" />
  <!-- Optional but recommended for efficient mapping -->
  <PackageReference Include="Mapster" Version="7.4.0" />
</ItemGroup>
```

### Project Configuration

Add these properties to your `.csproj` file:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
  <!-- Force analyzer to reload on every build to avoid caching issues -->
  <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
</PropertyGroup>
```

### Copy Generated Files for Inspection (Optional)

For debugging and verification, copy generated DTOs to your project:

```xml
<!-- Custom target to copy generated DTOs to project/GeneratedDtos folder -->
<Target Name="CopyGeneratedDtosToOutputFolder" AfterTargets="CoreCompile" 
        Condition="Exists('$(CompilerGeneratedFilesOutputPath)')">
    <ItemGroup>
        <GeneratedDtoFiles Include="$(CompilerGeneratedFilesOutputPath)\**\*Dto.g.cs"/>
    </ItemGroup>
    <MakeDir Directories="$(ProjectDir)GeneratedDtos" Condition="'@(GeneratedDtoFiles)' != ''"/>
    <Copy SourceFiles="@(GeneratedDtoFiles)"
          DestinationFiles="$(ProjectDir)GeneratedDtos\%(Filename)%(Extension)"
          SkipUnchangedFiles="false"
          OverwriteReadOnlyFiles="true"
          Condition="'@(GeneratedDtoFiles)' != ''"/>
    <Message Text="Copied %(Filename)%(Extension) to $(ProjectDir)GeneratedDtos" 
             Importance="high" Condition="'@(GeneratedDtoFiles)' != ''"/>
</Target>

<!-- Exclude generated DTOs from compilation, but keep them visible in Solution Explorer -->
<ItemGroup>
    <Compile Remove="GeneratedDtos\**\*.cs"/>
    <None Include="GeneratedDtos\**\*.cs"/>
</ItemGroup>
```

## Usage Examples

### Basic DTO Generation

```csharp
// Entity
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public DateTime CreatedAt { get; set; }
}

// DTO Declaration
[GenerateDto(typeof(Product))]
public partial record ProductDto;

// Usage
var product = await repository.GetByIdAsync(productId);
var dto = ProductDto.FromEntity(product);
return Results.Ok(dto);
```

### Excluding Properties

```csharp
// Exclude internal/sensitive properties
[GenerateDto(typeof(Product), Exclude = new[] { "StockQuantity", "CreatedAt" })]
public partial record ProductSummaryDto;

// Generated DTO will only include: Id, Name, Description, Price
```

### Custom Properties

```csharp
[GenerateDto(typeof(Product))]
public partial record ProductDto
{
    // Add computed property
    public string DisplayPrice => $"${Price:N2}";
    
    // Add custom property not in entity
    public bool IsAvailable => StockQuantity > 0;
}
```

### Collection Mapping

```csharp
// Map multiple entities to DTOs
var products = await repository.GetAllAsync();
var dtos = ProductDto.FromEntities(products);
return Results.Ok(dtos);

// Async with EF Core and Mapster
var dtos = await dbContext.Products
    .ProjectToType<ProductDto>() // Mapster extension
    .ToListAsync();
```

### Nested DTOs

```csharp
public class Order
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Customer Customer { get; set; } = null!;
    public List<OrderItem> Items { get; set; } = new();
    public OrderStatus Status { get; set; }
}

// Generate DTOs for related entities
[GenerateDto(typeof(Customer))]
public partial record CustomerDto;

[GenerateDto(typeof(OrderItem))]
public partial record OrderItemDto;

[GenerateDto(typeof(Order))]
public partial record OrderDto;

// Configure Mapster to map nested objects
TypeAdapterConfig<Order, OrderDto>
    .NewConfig()
    .Map(dest => dest.Customer, src => CustomerDto.FromEntity(src.Customer))
    .Map(dest => dest.Items, src => OrderItemDto.FromEntities(src.Items));
```

### API Request/Response DTOs

```csharp
// Read DTO
[GenerateDto(typeof(Customer))]
public partial record CustomerDto;

// Create DTO - exclude Id and timestamps
[GenerateDto(typeof(Customer), Exclude = new[] { "Id", "CreatedAt", "UpdatedAt" })]
public partial record CreateCustomerDto;

// Update DTO - exclude Id and CreatedAt
[GenerateDto(typeof(Customer), Exclude = new[] { "Id", "CreatedAt" })]
public partial record UpdateCustomerDto;

// API Endpoint
app.MapPost("/customers", async (CreateCustomerDto dto, ICustomerRepository repo) =>
{
    var customer = dto.ToEntity();
    await repo.AddAsync(customer);
    return Results.Created($"/customers/{customer.Id}", CustomerDto.FromEntity(customer));
});
```

## Mapster Integration

### Global Configuration

```csharp
// Configure mappings at startup
public static class MappingConfiguration
{
    public static void Configure()
    {
        TypeAdapterConfig.GlobalSettings.Scan(typeof(Program).Assembly);
        
        // Custom mapping rules
        TypeAdapterConfig<Customer, CustomerDto>
            .NewConfig()
            .Map(dest => dest.FullName, src => $"{src.FirstName} {src.LastName}")
            .Ignore(dest => dest.InternalId);
    }
}
```

### EF Core Query Projection

```csharp
// Efficient database queries with projection
public async Task<List<ProductDto>> GetProductsAsync()
{
    return await _dbContext.Products
        .Where(p => p.IsActive)
        .ProjectToType<ProductDto>() // Mapster projects directly from DB
        .ToListAsync();
}
```

### Custom Type Adapters

```csharp
// Register custom adapter for value objects
TypeAdapterConfig<Email, string>
    .NewConfig()
    .MapWith(email => email.Value);

TypeAdapterConfig<string, Email>
    .NewConfig()
    .MapWith(str => new Email(str));
```

## Advanced Patterns

### Version-Specific DTOs

```csharp
namespace MyApp.V1.Dtos
{
    [GenerateDto(typeof(Product))]
    public partial record ProductDto;
}

namespace MyApp.V2.Dtos
{
    [GenerateDto(typeof(Product), Exclude = new[] { "InternalCode" })]
    public partial record ProductDto
    {
        // V2 adds new computed field
        public string Category { get; init; } = string.Empty;
    }
}
```

### Read vs Write DTOs

```csharp
// Read model - all properties
[GenerateDto(typeof(Order))]
public partial record OrderDto;

// Command model - only writable properties
[GenerateDto(typeof(Order), Exclude = new[] { "Id", "OrderNumber", "CreatedAt", "Status" })]
public partial record CreateOrderCommand;

// Update model - fewer properties
[GenerateDto(typeof(Order), Exclude = new[] { "Id", "OrderNumber", "CreatedAt" })]
public partial record UpdateOrderCommand;
```

### Flattening Nested Objects

```csharp
public class Order
{
    public Guid Id { get; set; }
    public Address ShippingAddress { get; set; } = null!;
}

[GenerateDto(typeof(Order))]
public partial record OrderDto
{
    // Override to flatten
    public new string ShippingAddress { get; init; } = string.Empty;
}

// Configure flattening in Mapster
TypeAdapterConfig<Order, OrderDto>
    .NewConfig()
    .Map(dest => dest.ShippingAddress, 
         src => $"{src.ShippingAddress.Street}, {src.ShippingAddress.City}");
```

## Performance Considerations

### Compile-Time vs Runtime

DtoGenerator works at **compile time**, not runtime:
- ✅ No runtime reflection overhead
- ✅ No runtime code generation
- ✅ All mappings are statically compiled
- ✅ Full IDE IntelliSense support

### Mapping Performance

**With Mapster (Recommended)**:
- Uses compiled expressions for fast mapping
- Supports query projection for efficient database queries
- Caches mapping expressions

**Without Mapster (Fallback)**:
- Simple property-by-property assignment
- No reflection at runtime
- Suitable for simple scenarios

### Memory Efficiency

```csharp
// Efficient collection mapping with Mapster
var dtos = await dbContext.Products
    .ProjectToType<ProductDto>() // Maps in database, not in memory
    .ToListAsync();

// vs. inefficient approach
var products = await dbContext.Products.ToListAsync(); // Load all entities
var dtos = products.Select(p => ProductDto.FromEntity(p)); // Map in memory
```

## Troubleshooting

### Generated Files Not Visible

Ensure these properties are set:
```xml
<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
<CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
```

Check `obj/Generated` folder for generated files.

### Generator Not Running

- Clean and rebuild the solution
- Ensure `EnforceExtendedAnalyzerRules` is set to `true`
- Check for analyzer warnings in the Error List

### Compilation Errors

If you get duplicate property errors:
1. Check if properties are declared in both entity and DTO partial
2. Use the `Exclude` parameter to skip duplicates
3. Override properties explicitly if needed

### Mapster Not Detected

Ensure Mapster package reference is added:
```xml
<PackageReference Include="Mapster" Version="7.4.0" />
```

Generator checks for Mapster at compile time.

## Diagnostics

The generator reports `DKDTOGEN001` as a warning if generation fails for a target type. Common causes:

- Entity type not found or not accessible
- Generic entity types (limited support)
- Circular references in entity properties

Check build output for specific error messages.

## Limitations and Edge Cases

### Navigation Properties
Navigation and collection properties are included as shallow copies. For deep copying:
- Configure Mapster to handle nested objects
- Override properties in partial DTO
- Create separate DTOs for related entities

### Nullable Reference Types
Non-nullable reference type properties receive `= default!;` initializer to satisfy compiler null-state analysis.

### Generic Entities
Limited support for generic entity types. DTO shells must be non-generic.

### Inheritance
Entity inheritance is not automatically handled. Create separate DTOs for each entity type or use Mapster configuration.

## Best Practices

### 1. Use Partial Records
```csharp
// Preferred
[GenerateDto(typeof(Product))]
public partial record ProductDto;

// Instead of class
[GenerateDto(typeof(Product))]
public partial class ProductDto; // Works but records are more idiomatic for DTOs
```

### 2. Create Purpose-Specific DTOs
```csharp
[GenerateDto(typeof(Customer))]
public partial record CustomerDto; // Full read model

[GenerateDto(typeof(Customer), Exclude = new[] { "Id", "CreatedAt" })]
public partial record CreateCustomerDto; // Create command

[GenerateDto(typeof(Customer), Exclude = new[] { "Id", "Email" })]
public partial record CustomerSummaryDto; // List view
```

### 3. Organize DTOs by Feature
```
/Features
  /Customers
    /Entities
      Customer.cs
    /Dtos
      CustomerDto.cs
      CreateCustomerDto.cs
      UpdateCustomerDto.cs
  /Orders
    /Entities
      Order.cs
    /Dtos
      OrderDto.cs
```

### 4. Configure Mapster Globally
```csharp
// Startup.cs or Program.cs
var config = TypeAdapterConfig.GlobalSettings;
config.Scan(typeof(Program).Assembly);
config.RequireExplicitMapping = false;
config.RequireDestinationMemberSource = false;
```

### 5. Use DTOs at API Boundaries Only
```csharp
// ✅ Good - DTOs at API boundary
public async Task<CustomerDto> GetCustomerAsync(Guid id)
{
    var customer = await _repository.GetByIdAsync(id); // Domain entity internally
    return CustomerDto.FromEntity(customer); // Convert to DTO for API
}

// ❌ Bad - DTOs in domain layer
public async Task ProcessOrderAsync(OrderDto dto) // DTOs shouldn't be in domain services
```

## Integration with DKNet Framework

DtoGenerator works seamlessly with other DKNet components:

### With DKNet.EfCore.Repos
```csharp
public class CustomerService
{
    private readonly IReadRepository<Customer> _repository;
    
    public async Task<PagedList<CustomerDto>> GetCustomersAsync(int page, int pageSize)
    {
        return await _repository.Gets()
            .ProjectToType<CustomerDto>() // DtoGenerator + Mapster
            .ToPagedListAsync(page, pageSize); // DKNet.EfCore.Repos
    }
}
```

### With SlimBus CQRS
```csharp
public record GetCustomerQuery : IWitResponse<CustomerDto>
{
    public required Guid CustomerId { get; init; }
}

public class GetCustomerHandler : IHandler<GetCustomerQuery, CustomerDto>
{
    private readonly ICustomerRepository _repository;
    
    public async Task<CustomerDto?> OnHandle(GetCustomerQuery request, CancellationToken ct)
    {
        var customer = await _repository.FindAsync(request.CustomerId, ct);
        return customer != null ? CustomerDto.FromEntity(customer) : null;
    }
}
```

## Related Documentation

- **[DKNet.EfCore.Repos](./DKNet.EfCore.Repos.md)** - Repository pattern implementations
- **[DKNet.EfCore.Repos.Abstractions](./DKNet.EfCore.Repos.Abstractions.md)** - Repository abstractions
- **[Architecture Guide](../Architecture.md)** - Understanding DDD and Onion Architecture
- **[Examples & Recipes](../Examples/README.md)** - Practical implementation patterns

---

> 💡 **Pro Tip**: Use DtoGenerator for all API data transfer needs to maintain a clean separation between your domain model and external representations. Combined with Mapster, it provides a powerful, type-safe, and performant solution for object mapping in DDD applications.
