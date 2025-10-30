# EfCore.Specifications.Tests

Comprehensive unit tests for the DKNet.EfCore.Specifications library.

## Test Coverage

### DynamicPredicateBuilderTests

Tests for the `DynamicPredicateBuilder` class that builds dynamic LINQ predicates.

#### Build Tests

- ✅ Empty builder returns empty expression
- ✅ Single condition generates correct expression
- ✅ Multiple conditions combine with AND logic

#### Comparison Operations

- ✅ Equal operation with IQueryable
- ✅ NotEqual operation with IQueryable
- ✅ GreaterThan operation with IQueryable
- ✅ GreaterThanOrEqual operation with IQueryable
- ✅ LessThan operation with IQueryable
- ✅ LessThanOrEqual operation with IQueryable

#### String Operations

- ✅ Contains operation with IQueryable
- ✅ NotContains operation with IQueryable
- ✅ StartsWith operation with IQueryable
- ✅ EndsWith operation with IQueryable

#### Complex Queries

- ✅ Multiple conditions combined correctly
- ✅ Navigation properties (e.g., Category.Name)
- ✅ Date comparisons
- ✅ Projections with Select
- ✅ GroupBy operations

#### Validation

- ✅ Null property name throws ArgumentException
- ✅ Empty property name throws ArgumentException
- ✅ Whitespace property name throws ArgumentException

#### ToQuery Verification

- ✅ Equal operation generates correct SQL
- ✅ Multiple conditions generate AND in SQL
- ✅ String Contains generates LIKE in SQL
- ✅ Navigation properties generate JOIN in SQL

### DynamicPredicateExtensionsTests

Tests for the `DynamicPredicateExtensions` that integrate with LinqKit's ExpressionStarter.

#### DynamicAnd Tests

- ✅ Single condition combines with existing predicate
- ✅ Multiple conditions combine correctly
- ✅ String operations work with IQueryable
- ✅ Empty builder doesn't modify predicate
- ✅ Chained calls work correctly

#### DynamicOr Tests

- ✅ Single condition combines with existing predicate
- ✅ Multiple conditions combine correctly
- ✅ String operations work with IQueryable
- ✅ Chained calls work correctly

#### Mixed And/Or Tests

- ✅ Complex logic combining AND and OR
- ✅ Mixed dynamic and static predicates

#### Navigation Property Tests

- ✅ DynamicAnd with navigation properties
- ✅ DynamicOr with navigation properties

#### ToQuery Verification

- ✅ DynamicAnd generates correct SQL with AND
- ✅ DynamicOr generates correct SQL with OR
- ✅ String Contains generates LIKE
- ✅ Navigation properties generate JOIN

#### Integration Tests

- ✅ Complex e-commerce scenario (active products, price ranges, stock)
- ✅ Projections with Select
- ✅ GroupBy with aggregations
- ✅ OrderBy with paging (Skip/Take)
- ✅ Multiple string operations

## Test Data

The tests use Bogus to generate realistic test data:

- **Products**: 20 products with various prices, stock quantities, and categories
- **Categories**: 5 categories
- **Orders**: 15 orders with different statuses
- **OrderItems**: Multiple items per order
- **ProductTags**: Tags for featured products

## Key Testing Patterns

### Real SQL Server Testing with Testcontainers

All tests use **Testcontainers.MsSql** to spin up a real SQL Server 2022 container, ensuring the dynamic predicates work
correctly with actual SQL Server behavior, not just in-memory simulation. This provides:

- ✅ Real SQL Server query execution
- ✅ Actual database transactions
- ✅ True SQL generation and optimization
- ✅ Realistic constraint and index behavior
  All tests use EF Core's InMemoryDatabase to test against real IQueryable instances, ensuring the dynamic predicates
  work
  correctly with Entity Framework.

### ToQueryString Verification

Tests verify that the generated SQL contains expected clauses (WHERE, AND, OR, LIKE, JOIN), ensuring proper query
translation.

### AsExpandable Pattern

Tests use LinqKit's `AsExpandable()` extension to enable expression expansion for dynamic predicates:

```csharp
var predicate = PredicateBuilder.New<Product>()
    .And(p => p.IsActive)
    .DynamicAnd(builder => builder
        .With("Price", FilterOperations.GreaterThan, 100m));

var results = db.Products.AsExpandable().Where(predicate).ToList();
```

### Complex Scenarios

Tests cover real-world scenarios like:

- Finding active products with specific price ranges
- Combining multiple string searches
- Navigation property filtering
- Projections and aggregations

## Running Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~DynamicPredicateBuilderTests"

# Run with verbose output
dotnet test --verbosity detailed
```

## Test Structure

```
EfCore.Specifications.Tests/
├── Fixtures/
│   └── TestDbFixture.cs          # Shared test database setup
├── TestEntities/
│   ├── Entities.cs                # Test entity models
│   └── TestDbContext.cs           # Test DbContext
├── DynamicPredicateBuilderTests.cs
├── DynamicPredicateExtensionsTests.cs
└── GlobalUsings.cs
```

