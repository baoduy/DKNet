# Active Context

## Current Focus (Updated: Nov 5, 2025)

### 🎯 Primary Development Area: EfCore.Specifications Dynamic Predicate System

#### Recently Completed ✅
1. **Dynamic Predicate Builder Enhancement**
   - ✅ Implemented comprehensive enum validation with graceful fallback
   - ✅ Added property type resolution with camelCase/snake_case normalization
   - ✅ Fixed null handling in SQL queries (IS NULL / IS NOT NULL)
   - ✅ Operation adjustment for non-string types (Contains → Equal for enums/numbers)
   - ✅ Added `TryConvertToEnum` extension method with culture-invariant conversion
   - ✅ 32 unit tests for `TryConvertToEnum` method (100% passing)
   - ✅ Updated `DynamicAnd` and `DynamicOr` to handle null expressions gracefully

2. **Test Infrastructure**
   - ✅ Migrated from InMemory database to TestContainers.MsSql for integration tests
   - ✅ Created `TestDbFixture` with proper async lifecycle management
   - ✅ All 23 DynamicPredicateExtensionsTests passing with real SQL Server
   - ✅ Comprehensive test coverage for all FilterOperations
   - ✅ Test data generation using Bogus faker library

3. **Code Quality & Documentation**
   - ✅ XML documentation for all public APIs in Specifications project
   - ✅ Added detailed inline comments explaining complex algorithms
   - ✅ Fixed all code analysis warnings (CA1305, CA1310, etc.)
   - ✅ `TreatWarningsAsErrors=true` enforced across solution

### 🚀 Current Sprint Goals

#### Active Tasks
1. **Memory Bank Optimization** (In Progress)
   - 📝 Comprehensive project documentation for AI Copilot
   - 📝 Pattern catalog with code examples
   - 📝 Performance guidelines and anti-patterns
   - 📝 Test strategy documentation

2. **Code Coverage Improvement**
   - 🎯 Target: 90%+ coverage for EfCore.Specifications
   - 🎯 Add edge case tests for DynamicPredicateBuilder
   - 🎯 Integration tests for navigation property filtering
   - 🎯 Performance benchmarks for dynamic queries

#### Backlog
- [ ] Add support for case-insensitive string operations
- [ ] Implement custom filter operation extensibility
- [ ] Add query caching for repeated dynamic predicates
- [ ] Performance profiling and optimization
- [ ] Generate API documentation with DocFX

### 📊 Project Health Metrics

#### Test Coverage
- **EfCore.Specifications**: ~85% (target: 90%+)
- **Core.Extensions**: ~75% (target: 80%+)
- **AspCore.Tasks**: ~60% (needs improvement)

#### Code Quality
- ✅ Zero compilation warnings (TreatWarningsAsErrors=true)
- ✅ All analyzers enabled and enforced
- ✅ Nullable reference types enabled globally
- ✅ XML documentation complete for public APIs

#### Technical Debt
- ⚠️ Some StyleCop formatting warnings (non-blocking)
- ⚠️ Legacy code in older projects needs refactoring
- ⚠️ Need more integration tests for complex scenarios

### 🔧 Recent Technical Decisions

#### 1. TestContainers Over InMemory Database
**Rationale**: Real SQL Server behavior for accurate integration tests
- Catches SQL-specific issues (LIKE operators, date functions, etc.)
- Better confidence in production behavior
- Minimal performance impact with proper fixture lifecycle

#### 2. Null-Safe Dynamic Expressions
**Rationale**: Graceful degradation for invalid filter conditions
- Invalid enum values are silently skipped (don't break query)
- Null expressions return original predicate unchanged
- Better user experience for dynamic filtering scenarios

#### 3. Property Name Normalization
**Rationale**: Support multiple naming conventions in API inputs
- Accept camelCase, PascalCase, or snake_case property names
- Normalize to C# PascalCase internally
- Case-insensitive property matching for flexibility

### 📚 Documentation Standards

#### File Header Template (Mandatory)
```csharp
// <copyright file="FileName.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>
```

#### Test Naming Convention
```
MethodName_Scenario_ExpectedBehavior
```
Examples:
- `DynamicAnd_WithMultipleConditions_CombinesCorrectly`
- `TryConvertToEnum_WithInvalidStringValue_ShouldReturnFalse`
- `NormalizePropertyName_WithSnakeCase_ReturnsP ascalCase`

### 🎓 Key Learnings & Patterns

#### Pattern: Dynamic Predicate Building
```csharp
// Fluent API with type safety
var predicate = PredicateBuilder.New<Product>()
    .And(p => p.IsActive)
    .DynamicAnd(builder => builder
        .With("Price", FilterOperations.GreaterThan, 100m)
        .With("CategoryId", FilterOperations.Equal, categoryId))
    .DynamicOr(builder => builder
        .With("StockQuantity", FilterOperations.Equal, 0));

var results = dbContext.Products
    .AsNoTracking()
    .AsExpandable()
    .Where(predicate)
    .ToList();
```

#### Pattern: Test Fixture with TestContainers
```csharp
public class TestDbFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;
    public TestDbContext? Db { get; private set; }

    public async Task InitializeAsync()
    {
        _container = new MsSqlBuilder().Build();
        await _container.StartAsync();
        // Initialize and seed database
    }

    public async Task DisposeAsync()
    {
        await _container?.DisposeAsync();
    }
}
```

### 🔐 Security & Compliance
- ✅ No secrets in source code
- ✅ Audit logging enabled in EfCore extensions
- ✅ Data authorization for row-level security
- ✅ Encryption support for sensitive properties
- ✅ MIT License applied to all files

### 📦 NuGet Package Status
- Latest version format: `9.9.YYMMDD` (date-based semantic versioning)
- All packages build successfully
- Symbols packages generated for debugging

### 🤝 Collaboration Guidelines
1. **Before Starting**: Check memory-bank and active context
2. **During Development**: Follow established patterns and conventions
3. **Before Commit**: Run all tests, ensure zero warnings
4. **Documentation**: Update XML docs and memory bank if introducing new patterns
5. **Review**: Use GitHub PR with detailed description of changes
