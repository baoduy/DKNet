# Copilot Rules & Guidelines

## 🚨 Security Rules (CRITICAL)

### Never Upload Secrets
- ❌ No API keys, passwords, or connection strings in source code
- ❌ No `.env` files (use `.env.example` with placeholders)
- ✅ Use User Secrets for local development (`dotnet user-secrets`)
- ✅ Use Azure Key Vault or environment variables in production
- 🚨 **If a secret is leaked**: Rotate immediately, purge Git history, notify team

### Data Protection
- Always use encryption for sensitive data (PII, financial info)
- Enable audit logging for compliance requirements
- Implement row-level security with data authorization filters
- Use HTTPS for all API endpoints
- Never log sensitive information (passwords, tokens, PII)

## 📝 Code Style & Quality (MANDATORY)

### Compilation Standards
```xml
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<Nullable>enable</Nullable>
<LangVersion>latest</LangVersion>
```
- ✅ **Zero warnings**: All code must compile without warnings
- ✅ **Nullable types**: Use `?` for nullable reference types
- ✅ **Latest C#**: Take advantage of modern language features
- ✅ **XML docs**: All public APIs must have documentation

### Naming Conventions (Strict)
- **Classes/Structs/Enums**: `PascalCase` (e.g., `DynamicPredicateBuilder`)
- **Interfaces**: `IPascalCase` (e.g., `IRepository`, `ISpecification`)
- **Methods**: `PascalCase` with verb (e.g., `GetByIdAsync`, `ValidateInput`)
- **Properties**: `PascalCase` (e.g., `IsActive`, `CreatedDate`)
- **Private fields**: `_camelCase` with underscore (e.g., `_dbContext`, `_logger`)
- **Parameters/locals**: `camelCase` (e.g., `userId`, `filterCondition`)
- **Constants**: `PascalCase` (e.g., `MaxRetryAttempts`)

### File Organization
```
ProjectName/
├── Abstractions/      # Interfaces and abstract base classes
├── Extensions/        # Extension methods (static classes)
├── Models/           # Domain entities and DTOs
├── Services/         # Business logic
├── Specifications/   # Query specifications
└── README.md        # Project documentation
```

### File Headers (Required)
Every `.cs` file must start with:
```csharp
// <copyright file="FileName.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>
```

## 🏗️ Architecture Rules

### Entity Framework Core
```csharp
// ✅ DO: Use IQueryable for flexibility
public IQueryable<Product> GetProducts(Specification<Product> spec)
{
    return _dbContext.Products.ApplySpecification(spec);
}

// ✅ DO: Use AsNoTracking for read-only queries
var products = _dbContext.Products
    .AsNoTracking()
    .Where(p => p.IsActive)
    .ToList();

// ✅ DO: Use async/await for database operations
public async Task<Product?> GetByIdAsync(int id)
{
    return await _dbContext.Products.FindAsync(id);
}

// ❌ DON'T: Materialize early (performance issue)
var allProducts = _dbContext.Products.ToList(); // Loads everything!
var filtered = allProducts.Where(p => p.Price > 100);

// ❌ DON'T: Use IEnumerable for database queries
IEnumerable<Product> GetProducts() // Returns IEnumerable
{
    return _dbContext.Products; // Loses query composition
}
```

### Specification Pattern
```csharp
// ✅ DO: Create reusable specifications
public class ActiveProductsSpec : Specification<Product>
{
    public override Expression<Func<Product, bool>> Criteria => 
        p => p.IsActive && p.StockQuantity > 0;
}

// ✅ DO: Compose specifications
var predicate = PredicateBuilder.New<Product>()
    .And(p => p.IsActive)
    .DynamicAnd(builder => builder
        .With("CategoryId", FilterOperations.Equal, categoryId));

// ❌ DON'T: Put business logic in repositories
// Business logic belongs in services, not data access layer
```

### Dependency Injection
```csharp
// ✅ DO: Constructor injection
public class ProductService
{
    private readonly IRepository<Product> _repository;
    private readonly ILogger<ProductService> _logger;
    
    public ProductService(
        IRepository<Product> repository, 
        ILogger<ProductService> logger)
    {
        _repository = repository;
        _logger = logger;
    }
}

// ✅ DO: Register with appropriate lifetime
services.AddScoped<IRepository<Product>, Repository<Product>>();
services.AddSingleton<IConfiguration>(configuration);
services.AddTransient<IEmailService, EmailService>();

// ❌ DON'T: Use service locator pattern
var service = serviceProvider.GetService<IProductService>(); // Anti-pattern
```

## 🧪 Testing Rules

### Test Structure (Arrange-Act-Assert)
```csharp
[Fact]
public void DynamicAnd_WithMultipleConditions_CombinesCorrectly()
{
    // Arrange: Set up test data and dependencies
    var predicate = PredicateBuilder.New<Product>()
        .And(p => p.IsActive);
    
    // Act: Execute the operation under test
    predicate = predicate.DynamicAnd(builder => builder
        .With("Price", FilterOperations.GreaterThan, 50m)
        .With("StockQuantity", FilterOperations.GreaterThan, 5));
    var results = _db.Products.Where(predicate).ToList();
    
    // Assert: Verify expected outcomes
    results.ShouldNotBeEmpty();
    results.ShouldAllBe(p => p.IsActive && p.Price > 50m && p.StockQuantity > 5);
}
```

### Test Naming Convention
```
MethodName_Scenario_ExpectedBehavior
```
Examples:
- `GetByIdAsync_WithValidId_ReturnsProduct`
- `CreateDynamicExpression_WithNullBuilder_ReturnsNull`
- `TryConvertToEnum_WithInvalidValue_ReturnsFalse`

### Test Best Practices
- ✅ Use TestContainers for EF Core integration tests (real database)
- ✅ Use in-memory database only for fast unit tests
- ✅ Use Bogus for generating test data
- ✅ Use Shouldly for fluent assertions
- ✅ Test both happy path and edge cases
- ✅ Each test should be independent (no shared state)
- ✅ Clear test database state between tests
- ❌ Don't test framework code (EF Core, LINQ, etc.)
- ❌ Don't create brittle tests that break on refactoring

### Test Coverage Goals
- **Critical paths**: 90%+ coverage
- **Business logic**: 85%+ coverage
- **Data access**: 80%+ coverage
- **Extensions/utilities**: 75%+ coverage

## 🚀 Performance Rules

### Query Optimization
```csharp
// ✅ DO: Filter before loading
var products = await _dbContext.Products
    .Where(p => p.IsActive)
    .ToListAsync();

// ✅ DO: Use projections for DTOs
var productDtos = await _dbContext.Products
    .Where(p => p.IsActive)
    .Select(p => new ProductDto 
    { 
        Id = p.Id, 
        Name = p.Name, 
        Price = p.Price 
    })
    .ToListAsync();

// ✅ DO: Use Include for known relationships
var productsWithCategory = await _dbContext.Products
    .Include(p => p.Category)
    .Where(p => p.IsActive)
    .ToListAsync();

// ❌ DON'T: Cause N+1 queries
foreach (var product in products)
{
    var category = await _dbContext.Categories
        .FindAsync(product.CategoryId); // N+1!
}

// ❌ DON'T: Load unnecessary data
var allColumns = await _dbContext.Products
    .ToListAsync(); // Loads everything including large text fields
```

### Async/Await Best Practices
```csharp
// ✅ DO: Use async all the way
public async Task<List<Product>> GetActiveProductsAsync()
{
    return await _dbContext.Products
        .Where(p => p.IsActive)
        .ToListAsync();
}

// ✅ DO: Use ConfigureAwait(false) in libraries
await _dbContext.SaveChangesAsync()
    .ConfigureAwait(false);

// ❌ DON'T: Mix sync and async (deadlock risk)
public Product GetProduct(int id)
{
    return _dbContext.Products
        .FindAsync(id).Result; // DEADLOCK RISK!
}

// ❌ DON'T: Use async void (except event handlers)
public async void ProcessOrder(int orderId) // Bad!
{
    await _orderService.ProcessAsync(orderId);
}
```

## 📚 Documentation Rules

### XML Documentation (Required)
```csharp
/// <summary>
///     Combines the existing predicate with a new dynamic condition using AND logic.
///     If the dynamic expression is null or empty, the original predicate is returned unchanged.
/// </summary>
/// <typeparam name="T">The type of the entity being queried</typeparam>
/// <param name="predicate">The existing expression to extend</param>
/// <param name="builder">Action that configures the dynamic filter conditions</param>
/// <returns>
///     The extended expression with the AND condition applied, 
///     or the original predicate if the dynamic expression is null
/// </returns>
/// <exception cref="ArgumentNullException">Thrown when predicate is null</exception>
/// <example>
///     <code>
///     var predicate = PredicateBuilder.New&lt;Product&gt;()
///         .And(p => p.IsActive)
///         .DynamicAnd(builder => builder
///             .With("Price", FilterOperations.GreaterThan, 100m));
///     </code>
/// </example>
public static Expression<Func<T, bool>> DynamicAnd<T>(
    this Expression<Func<T, bool>> predicate,
    Action<DynamicPredicateBuilder<T>> builder)
{
    // Implementation...
}
```

### Inline Comments
- ✅ Explain **why**, not **what**
- ✅ Document complex algorithms
- ✅ Warn about gotchas or non-obvious behavior
- ❌ Don't state the obvious
- ❌ Don't leave commented-out code

```csharp
// ✅ Good: Explains why
// We skip invalid enum values to avoid breaking the entire query
// This allows partial filtering even when some user inputs are invalid
if (!IsValidEnumValue(value, enumType))
    continue;

// ❌ Bad: States the obvious
// Loop through all conditions
foreach (var condition in conditions)
{
    // Add condition to builder
    builder.Add(condition);
}
```

## 🔄 Git & Version Control

### Commit Messages
```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types**: feat, fix, docs, refactor, test, chore, perf
**Examples**:
```
feat(specifications): add enum validation to dynamic predicates

- Validate enum values before applying filters
- Skip invalid enum values with graceful fallback
- Add 15 unit tests for TryConvertToEnum method

Closes #123
```

### Branch Strategy
- `main`: Production-ready code
- `dev`: Integration branch
- `feature/xxx`: New features
- `fix/xxx`: Bug fixes
- `refactor/xxx`: Code improvements

### Pull Request Guidelines
- ✅ Link to related issue/task
- ✅ Include tests for new functionality
- ✅ Update documentation
- ✅ All tests passing
- ✅ Zero warnings/errors
- ✅ Code review from at least one team member

## 🎯 AI Copilot Specific Guidelines

### When Using AI Copilot
1. **Check memory-bank first**: Review activeContext, systemPatterns, techContext
2. **Follow established patterns**: Don't invent new patterns without discussion
3. **Maintain consistency**: Match existing code style and naming
4. **Verify generated code**: AI can make mistakes - always review
5. **Run tests**: Ensure AI-generated code passes all tests
6. **Update documentation**: If AI generates new patterns, document them

### What to Accept from AI
- ✅ Boilerplate code (constructors, properties)
- ✅ Test data generation
- ✅ XML documentation
- ✅ Repetitive refactoring
- ✅ Unit test scaffolding

### What to Review Carefully
- ⚠️ Business logic
- ⚠️ Security-sensitive code
- ⚠️ Database queries and performance
- ⚠️ Error handling and edge cases
- ⚠️ Public API design

### What to Never Accept Blindly
- ❌ Code with secrets/credentials
- ❌ Code that bypasses validation
- ❌ Code with obvious bugs
- ❌ Code that violates architecture patterns
- ❌ Code without tests

## 📋 Pre-Commit Checklist

Before committing code, verify:
- [ ] Code compiles without warnings
- [ ] All tests pass
- [ ] XML documentation added for public APIs
- [ ] File headers present
- [ ] No secrets or sensitive data
- [ ] Follows naming conventions
- [ ] Follows established patterns
- [ ] Performance considerations addressed
- [ ] Error handling implemented
- [ ] Logging added where appropriate

## 🆘 When in Doubt

1. **Check memory-bank**: Most questions answered in documentation
2. **Look at examples**: Find similar code in the codebase
3. **Ask the team**: Better to ask than to guess wrong
4. **Write a test**: Tests clarify requirements
5. **Start simple**: Can always refactor later

## 📞 Resources

- **Memory Bank**: `/memory-bank/` directory
- **Project Patterns**: `systemPatterns.md`
- **Active Work**: `activeContext.md`
- **Tech Stack**: `techContext.md`
- **Product Info**: `productContext.md`
