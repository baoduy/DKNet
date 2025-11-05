# AI Copilot Quick Reference Guide

## 🎯 Start Here

When working on this codebase, always:
1. Read `activeContext.md` first - know what's being worked on
2. Check `systemPatterns.md` - follow established patterns
3. Review `copilot-rules.md` - follow coding standards
4. Reference `techContext.md` - understand technology constraints

## 🚀 Common Tasks

### Adding a New Dynamic Filter Operation

1. **Update FilterOperations enum** in `DynamicPredicateBuilder.cs`
2. **Add case** in `BuildClause()` method
3. **Add case** in `AdjustOperationForValueType()` if needed
4. **Write tests** in `DynamicPredicateExtensionsTests.cs`
5. **Update XML docs** with new operation example

### Creating a New Specification

```csharp
public class MySpecification : Specification<MyEntity>
{
    public override Expression<Func<MyEntity, bool>> Criteria => 
        entity => entity.IsActive && entity.SomeProperty > 0;
        
    public MySpecification()
    {
        // Optional: Add includes
        AddInclude(e => e.RelatedEntity);
        
        // Optional: Add ordering
        AddOrderBy(e => e.Name);
    }
}
```

### Writing a Test

```csharp
[Fact]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange: Setup
    var predicate = PredicateBuilder.New<Product>()
        .And(p => p.IsActive);
    
    // Act: Execute
    var results = _db.Products.Where(predicate).ToList();
    
    // Assert: Verify
    results.ShouldNotBeEmpty();
    results.ShouldAllBe(p => p.IsActive);
}
```

## ⚡ Quick Commands

### Build & Test
```bash
# Build with zero warnings
dotnet build

# Run all tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~TestName"

# Code coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Code Quality
```bash
# Format code
dotnet format

# Check analyzers
dotnet build /p:TreatWarningsAsErrors=true
```

## 🎨 Code Snippets

### Extension Method Template
```csharp
/// <summary>
///     Brief description of what this does.
/// </summary>
/// <typeparam name="T">Generic type description</typeparam>
/// <param name="source">Parameter description</param>
/// <returns>Return value description</returns>
public static TResult MethodName<T>(this T source)
{
    // Implementation
}
```

### Repository Method Template
```csharp
public async Task<TEntity?> GetByIdAsync(int id)
{
    return await _dbContext.Set<TEntity>()
        .AsNoTracking()
        .FirstOrDefaultAsync(e => e.Id == id);
}
```

### Service Method Template
```csharp
public async Task<Result<TDto>> ProcessAsync(int id)
{
    try
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            return Result.NotFound();
            
        // Business logic here
        
        return Result.Success(dto);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing {Id}", id);
        return Result.Error(ex.Message);
    }
}
```

## 📋 Checklist Before Commit

- [ ] Code compiles without warnings
- [ ] All tests pass
- [ ] XML docs on public APIs
- [ ] File header present
- [ ] Follows naming conventions
- [ ] No secrets in code
- [ ] Performance considered
- [ ] Error handling implemented

## 🔍 Common Patterns at a Glance

### EF Core Query Pattern
```csharp
var results = await _dbContext.Products
    .AsNoTracking()                    // Read-only
    .Where(p => p.IsActive)           // Filter
    .Include(p => p.Category)          // Eager load
    .OrderBy(p => p.Name)             // Sort
    .Skip(skip).Take(take)            // Paging
    .ToListAsync();                   // Execute
```

### Dynamic Predicate Pattern
```csharp
var predicate = PredicateBuilder.New<Product>()
    .And(p => p.IsActive)
    .DynamicAnd(builder => builder
        .With("Price", FilterOperations.GreaterThan, 100m)
        .With("CategoryId", FilterOperations.Equal, catId))
    .DynamicOr(builder => builder
        .With("StockQuantity", FilterOperations.Equal, 0));

var results = _db.Products
    .AsExpandable()                    // Required for LinqKit
    .Where(predicate)
    .ToList();
```

### Dependency Injection Pattern
```csharp
// Registration
services.AddScoped<IRepository<Product>, Repository<Product>>();

// Usage
public class ProductService
{
    private readonly IRepository<Product> _repository;
    
    public ProductService(IRepository<Product> repository)
    {
        _repository = repository;
    }
}
```

## 🎓 Key Principles

1. **Type Safety First**: Use strong typing, avoid dynamic/object when possible
2. **Async All the Way**: Database operations must be async
3. **Filter Early**: Push filtering to database with WHERE clauses
4. **Test Everything**: Aim for 85%+ coverage
5. **Document Public APIs**: XML docs are mandatory
6. **Performance Matters**: Profile before optimizing, but be aware
7. **Null Safety**: Use nullable reference types, check for nulls
8. **Follow Patterns**: Don't invent new patterns without discussion

## ⚠️ Common Pitfalls to Avoid

❌ **Don't**:
```csharp
// N+1 query
foreach (var product in products)
{
    var category = await _db.Categories.FindAsync(product.CategoryId);
}

// Premature materialization
var list = _db.Products.ToList().Where(p => p.Price > 100);

// Async void
public async void ProcessOrder() { }

// Sync over async (deadlock risk)
var result = GetAsync().Result;
```

✅ **Do**:
```csharp
// Include relationships
var products = await _db.Products
    .Include(p => p.Category)
    .ToListAsync();

// Filter on database
var filtered = await _db.Products
    .Where(p => p.Price > 100)
    .ToListAsync();

// Async Task
public async Task ProcessOrderAsync() { }

// Await properly
var result = await GetAsync();
```

## 📞 Quick Links

- **Main Patterns**: `systemPatterns.md`
- **Current Work**: `activeContext.md`
- **Full Rules**: `copilot-rules.md`
- **Tech Stack**: `techContext.md`
- **Product Info**: `productContext.md`
- **Progress**: `progress-detailed.md`

## 🆘 When Stuck

1. Search existing code for similar examples
2. Check test files for usage patterns
3. Review XML documentation on related classes
4. Consult memory-bank documentation
5. Ask the team if still unclear

---

**Remember**: This codebase values **correctness** > **speed** > **cleverness**

Write code that is:
- ✅ Correct (works as intended)
- ✅ Tested (proven to work)
- ✅ Readable (others can understand)
- ✅ Maintainable (easy to change)
- ✅ Performant (but not prematurely optimized)

