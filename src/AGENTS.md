# DKNet Framework - AI Agent Guidelines

> **CRITICAL**: Read `/memory-bank/README.md` FIRST before any work. This document is a quick reference. Full guidelines are in the memory bank.

## 🎯 Quick Context

**Project**: DKNet Framework - .NET 9 library collection for EF Core extensions, background tasks, and messaging  
**Current Focus**: Dynamic Predicate System with Specification Pattern (see `activeContext.md`)  
**Key Patterns**: Specification, Repository, Dynamic Predicate Builder, TestContainers integration  
**Standards**: Zero warnings (`TreatWarningsAsErrors=true`), nullable types enabled, XML docs required

## 📚 Before You Start (REQUIRED)

1. **Read Memory Bank**: `/memory-bank/README.md` → navigation to all docs
2. **Check Current Work**: `/memory-bank/activeContext.md` → what we're working on
3. **Learn Patterns**: `/memory-bank/systemPatterns.md` → how we code
4. **Follow Rules**: `/memory-bank/copilot-rules.md` → coding standards (8000+ words)
5. **Quick Reference**: `/memory-bank/copilot-quick-reference.md` → common tasks

## Project Structure & Module Organization
- `DKNet.FW.sln` aggregates the libraries under `Core`, `Services`, `EfCore`, `SlimBus`, and the ASP.NET host in `DKNet.AspCore.Tasks`. Keep shared abstractions in `Core` and feature-specific code near its consumer to limit cross-module coupling.
- Background services and Aspire configurations live in `Aspire`, while reusable templates and NuGet packaging assets sit in `Templates`, `memory-bank`, and `NugetLogo.png`.
- Tests are rooted in `AspCore.Tasks.Tests`; generated artefacts such as `TestResults/` and `nupkgs/` should stay out of source control.

## Build, Test, and Development Commands
```bash
dotnet restore DKNet.FW.sln         # Restore solution dependencies
dotnet build DKNet.FW.sln -c Debug  # Validate compilation before committing
dotnet test DKNet.FW.sln --settings coverage.runsettings --collect:"XPlat Code Coverage"
dotnet test AspCore.Tasks.Tests     # Targeted run while iterating
./nuget.sh pack && ./verify_nuget_package.sh  # Produce + sanity-check packages
```

## Coding Style & Naming Conventions

### General Rules
- Follow the solution-wide StyleCop rules (`stylecop.json`) and `.editorconfig` exemptions
- Private fields: `_camelCase` with underscore prefix (e.g., `_dbContext`, `_logger`)
- Methods: `PascalCase` with `Async` suffix for async methods (e.g., `GetByIdAsync`)
- Properties: `PascalCase` (e.g., `IsActive`, `FilterOperations`)
- Parameters/locals: `camelCase` (e.g., `predicate`, `builder`)
- Classes/Interfaces: `PascalCase`, interfaces with `I` prefix (e.g., `IRepository`, `DynamicPredicateBuilder`)
- Run `dotnet format` before opening a PR to enforce analyzer-driven layout and ordering

### DKNet-Specific Conventions
- **Test naming**: `MethodName_Scenario_ExpectedBehavior` (e.g., `DynamicAnd_WithMultipleConditions_CombinesCorrectly`)
- **File headers**: All `.cs` files require copyright header (see `FILE_HEADER_TEMPLATE.md`)
- **XML docs**: Mandatory for all public APIs (methods, classes, properties)
- **Extension methods**: Put in static classes in `/Extensions` folder
- **Specifications**: Inherit from `Specification<TEntity>` base class
- **Null safety**: Use nullable reference types (`?`), handle nulls gracefully

### Key Patterns to Follow

#### Dynamic Predicate Pattern
```csharp
// ✅ Correct: Composable, null-safe dynamic predicates
var predicate = PredicateBuilder.New<Product>()
    .And(p => p.IsActive)
    .DynamicAnd(builder => builder
        .With("Price", FilterOperations.GreaterThan, 100m)
        .With("CategoryId", FilterOperations.Equal, categoryId));

var results = _db.Products
    .AsNoTracking()
    .AsExpandable()  // Required for LinqKit
    .Where(predicate)
    .ToList();
```

#### EF Core Query Pattern
```csharp
// ✅ Correct: Async, AsNoTracking for read-only, filter on DB
public async Task<List<Product>> GetActiveProductsAsync()
{
    return await _dbContext.Products
        .AsNoTracking()
        .Where(p => p.IsActive && p.Price > 0)
        .OrderBy(p => p.Name)
        .ToListAsync();
}

// ❌ Wrong: Sync, no tracking consideration, filter in memory
public List<Product> GetActiveProducts()
{
    return _dbContext.Products.ToList()
        .Where(p => p.IsActive).ToList();
}
```

#### Test Pattern with TestContainers
```csharp
// ✅ Correct: TestContainers, Arrange-Act-Assert, descriptive name
[Fact]
public void DynamicAnd_WithEnumFilter_FiltersCorrectly()
{
    // Arrange
    var predicate = PredicateBuilder.New<Product>()
        .And(p => p.IsActive)
        .DynamicAnd(b => b.With("Status", FilterOperations.Equal, "Active"));
    
    // Act
    var results = _db.Products.Where(predicate).ToList();
    
    // Assert
    results.ShouldNotBeEmpty();
    results.ShouldAllBe(p => p.IsActive);
}
```

## Testing Guidelines

### Framework & Tools
- **Test Framework**: xUnit (required)
- **Assertions**: Shouldly (fluent assertions - `results.ShouldNotBeEmpty()`)
- **Integration Tests**: TestContainers.MsSql (real SQL Server)
- **Test Data**: Bogus faker for realistic data generation
- **Mocking**: Avoid where possible; prefer real implementations

### Test Structure (Arrange-Act-Assert)
```csharp
[Fact]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange: Setup test data and dependencies
    var fixture = new TestDbFixture();
    var predicate = PredicateBuilder.New<Product>();
    
    // Act: Execute the operation under test
    var result = _service.ProcessAsync(predicate);
    
    // Assert: Verify expected outcomes
    result.ShouldNotBeNull();
    result.ShouldBe(expectedValue);
}
```

### DKNet Testing Best Practices
- **Naming**: `MethodName_Scenario_ExpectedBehavior` (e.g., `TryConvertToEnum_WithInvalidValue_ReturnsFalse`)
- **Isolation**: Each test manages its own database state (use TestContainers)
- **Deterministic**: No random data without seeded values
- **Fast**: Unit tests should complete in milliseconds
- **Real DB**: Use TestContainers for EF Core integration tests (not InMemory)
- **Coverage**: Target 85%+ for business logic, 90%+ for critical paths
- **Edge cases**: Test null values, empty collections, invalid inputs
- **SQL verification**: Use `.ToQueryString()` to verify generated SQL

### Common Test Patterns

#### TestContainers Fixture
```csharp
public class TestDbFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;
    public TestDbContext Db { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new MsSqlBuilder()
            .WithPassword("YourStrong@Passw0rd")
            .Build();
        await _container.StartAsync();
        
        // Create and seed database
        Db = CreateDbContext(_container.GetConnectionString());
        await Db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await Db.DisposeAsync();
        await _container!.DisposeAsync();
    }
}
```

#### Testing Dynamic Predicates
```csharp
[Fact]
public void DynamicAnd_WithMultipleConditions_GeneratesCorrectSQL()
{
    // Arrange
    _db.ChangeTracker.Clear();
    var predicate = PredicateBuilder.New<Product>()
        .And(p => p.IsActive)
        .DynamicAnd(builder => builder
            .With("Price", FilterOperations.GreaterThan, 50m)
            .With("StockQuantity", FilterOperations.GreaterThan, 5));
    
    // Act
    var query = _db.Products.Where(predicate);
    var sql = query.ToQueryString();
    var results = query.ToList();
    
    // Assert
    sql.ShouldContain("WHERE");
    sql.ShouldContain("[p].[IsActive]");
    sql.ShouldContain("[p].[Price] > ");
    results.ShouldAllBe(p => p.IsActive && p.Price > 50m && p.StockQuantity > 5);
}
```

### Coverage Requirements
- **Critical paths**: 90%+ coverage (e.g., DynamicPredicateBuilder, Specifications)
- **Business logic**: 85%+ coverage (e.g., Repository methods, Services)
- **Extensions/utilities**: 80%+ coverage (e.g., TypeExtensions)
- **Infrastructure**: 75%+ coverage (e.g., Configuration, Startup)
- Run: `dotnet test --collect:"XPlat Code Coverage"`
- Include coverage diffs in PR discussions when regression is possible

## Commit & Pull Request Guidelines

### Commit Message Format
```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types**: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `perf`  
**Scopes**: `specifications`, `repository`, `extensions`, `tests`, `docs`

**Examples**:
```
feat(specifications): add enum validation to dynamic predicates

- Validate enum values before applying filters
- Skip invalid enum values with graceful fallback
- Add 15 unit tests for TryConvertToEnum method

Closes #123
```

```
fix(tests): migrate from InMemory to TestContainers

- Replace InMemory database with TestContainers.MsSql
- Update all integration tests for real SQL Server
- Fix SQL-specific query issues caught by real database

BREAKING CHANGE: Tests now require Docker
```

### Pull Request Requirements
- **Problem statement**: What issue does this solve?
- **Summary of changes**: Key modifications and additions
- **Validation**: Test results, SQL query strings, coverage reports
- **Breaking changes**: Clearly marked in PR body
- **Linked issues**: Reference related issues/tasks
- **Screenshots**: For UI changes (if applicable)
- **Documentation**: Update memory bank if patterns change

### Pre-PR Checklist
- [ ] All tests pass (`dotnet test`)
- [ ] Zero compilation warnings (`dotnet build`)
- [ ] Code formatted (`dotnet format`)
- [ ] XML docs added for public APIs
- [ ] Memory bank updated if needed (especially `activeContext.md`)
- [ ] Coverage maintained or improved
- [ ] No secrets or sensitive data in code

### Review Process
- Request review from module owners (`Core`, `EfCore`, `Services`)
- Address all comments before merging
- Squash commits if needed for clean history
- Update `activeContext.md` after merge

## ⚠️ Common Pitfalls to Avoid

### EF Core Anti-Patterns
```csharp
// ❌ N+1 Query Problem
foreach (var product in products)
{
    var category = await _db.Categories.FindAsync(product.CategoryId);
}

// ✅ Use Include or projection
var products = await _db.Products
    .Include(p => p.Category)
    .ToListAsync();

// ❌ Premature materialization
var list = _db.Products.ToList().Where(p => p.Price > 100);

// ✅ Filter on database
var filtered = await _db.Products
    .Where(p => p.Price > 100)
    .ToListAsync();

// ❌ Sync over async (deadlock risk!)
var result = GetAsync().Result;

// ✅ Await properly
var result = await GetAsync();
```

### Dynamic Predicate Pitfalls
```csharp
// ❌ Forgetting AsExpandable (LinqKit requirement)
var results = _db.Products.Where(predicate).ToList(); // May fail

// ✅ Always use AsExpandable with dynamic predicates
var results = _db.Products.AsExpandable().Where(predicate).ToList();

// ❌ Not handling null results
var expr = CreateDynamicExpression(builder);
predicate.And(expr); // expr might be null!

// ✅ Null-safe operation
var expr = CreateDynamicExpression(builder);
if (expr != null) predicate = predicate.And(expr);
// OR: Use the built-in null handling
predicate = predicate.DynamicAnd(builder); // Handles null internally
```

### Testing Pitfalls
```csharp
// ❌ Using InMemory for SQL-specific features
var options = new DbContextOptionsBuilder<TestDbContext>()
    .UseInMemoryDatabase("TestDb")
    .Options;

// ✅ Use TestContainers for real SQL Server
var container = new MsSqlBuilder().Build();
await container.StartAsync();

// ❌ Shared state between tests
public class MyTests : IClassFixture<SharedFixture>
{
    // Tests may interfere with each other
}

// ✅ Isolated test data
public class MyTests : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        // Create fresh container for each test
    }
}
```

### Security & Quality Pitfalls
```csharp
// ❌ Secrets in code
var connectionString = "Server=prod;Password=secret123;";

// ✅ Use configuration
var connectionString = _configuration.GetConnectionString("Default");

// ❌ Missing null checks
public void Process(Product product)
{
    var name = product.Category.Name; // NullReferenceException!
}

// ✅ Null-safe with nullable types
public void Process(Product? product)
{
    if (product?.Category == null) return;
    var name = product.Category.Name;
}
```

## 🚀 Quick Commands Reference

```bash
# Build & restore
dotnet restore DKNet.FW.sln
dotnet build DKNet.FW.sln -c Debug

# Run all tests
dotnet test DKNet.FW.sln

# Run specific test project
dotnet test EfCore.Specifications.Tests

# Run specific test
dotnet test --filter "FullyQualifiedName~DynamicAnd_WithMultipleConditions"

# Code coverage
dotnet test --collect:"XPlat Code Coverage" --settings coverage.runsettings

# Format code
dotnet format

# NuGet package
./nuget.sh pack && ./verify_nuget_package.sh

# Check for warnings
dotnet build /p:TreatWarningsAsErrors=true
```

## 📚 Additional Resources

- **Memory Bank**: `/memory-bank/README.md` - Start here!
- **Quick Reference**: `/memory-bank/copilot-quick-reference.md`
- **Patterns**: `/memory-bank/systemPatterns.md`
- **Full Rules**: `/memory-bank/copilot-rules.md` (8000+ words)
- **Current Work**: `/memory-bank/activeContext.md`
- **Tech Stack**: `/memory-bank/techContext.md`

---

**Remember**: The memory bank contains comprehensive documentation. This AGENTS.md is just a quick reference. Always check the memory bank for detailed guidelines!
