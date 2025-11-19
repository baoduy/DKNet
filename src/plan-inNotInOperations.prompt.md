# Plan: Add `In` and `NotIn` Array Operations to DynamicOperations

## Overview

Add support for SQL `IN` and `NOT IN` operations to the dynamic predicate system using **System.Linq.Dynamic.Core's `DynamicExpressionParser.ParseLambda`** exclusively. This ensures parameterized queries and prevents SQL injection while maintaining the existing safety patterns.

**Security Requirement**: All expression building **MUST** go through `DynamicExpressionParser.ParseLambda` with values as parameters (`@0`, `@1`, etc.) - never string concatenation or manual expression tree construction for user input.

## Goals

1. Enable runtime filtering against collections of values (e.g., `WHERE CategoryId IN (1, 2, 3)`)
2. Maintain type safety and null-safe graceful degradation patterns
3. Ensure EF Core translates expressions to optimal SQL `IN` clauses
4. Prevent SQL injection by exclusively using `DynamicExpressionParser.ParseLambda`
5. Support arrays, lists, and any `IEnumerable<T>` collection types
6. Maintain consistency with existing `DynamicOperations` patterns

## Implementation Steps

### Step 1: Add Enum Values to `DynamicOperations.cs`

**File**: `/Users/steven/_CODE/DRUNK/DKNet/src/EfCore/DKNet.EfCore.Specifications/Dynamics/DynamicOperations.cs`

**Changes**:
- Add `In = 10` enum member with XML documentation
- Add `NotIn = 11` enum member with XML documentation
- Document that these operations require array/collection values (`IEnumerable<T>`)

**Example**:
```csharp
/// <summary>
///     Checks if the property value is contained in a collection of values.
///     Requires value to be an array or IEnumerable (excluding string).
///     Translates to SQL IN clause.
/// </summary>
In = 10,

/// <summary>
///     Checks if the property value is NOT contained in a collection of values.
///     Requires value to be an array or IEnumerable (excluding string).
///     Translates to SQL NOT IN clause.
/// </summary>
NotIn = 11
```

### Step 2: Add Array Validation Helper to `DynamicPredicateBuilderExtensions.cs`

**File**: `/Users/steven/_CODE/DRUNK/DKNet/src/EfCore/DKNet.EfCore.Specifications/Dynamics/DynamicPredicateBuilderExtensions.cs`

**Changes**:
- Add `ValidateArrayValue` internal extension method
- Check if value implements `IEnumerable` (excluding `string`)
- Verify collection is non-empty
- Return `false` for null, empty, or non-enumerable values

**Implementation**:
```csharp
/// <summary>
///     Validates if a value is a valid array/collection for In/NotIn operations.
///     Returns false for null, empty collections, or non-enumerable types (including string).
/// </summary>
/// <param name="value">The value to validate</param>
/// <param name="operation">The operation being performed</param>
/// <returns>True if value is valid for the operation, false otherwise</returns>
internal static bool ValidateArrayValue(object? value, DynamicOperations operation)
{
    // Only validate for In/NotIn operations
    if (operation is not (DynamicOperations.In or DynamicOperations.NotIn))
        return true;

    if (value == null)
        return false;

    // String implements IEnumerable but should not be treated as array
    if (value is string)
        return false;

    // Check if value is enumerable
    if (value is not IEnumerable enumerable)
        return false;

    // Check if collection is non-empty
    var enumerator = enumerable.GetEnumerator();
    var hasElements = enumerator.MoveNext();
    (enumerator as IDisposable)?.Dispose();

    return hasElements;
}
```

### Step 3: Update `BuildClause` Method in `DynamicPredicateBuilderExtensions.cs`

**File**: `/Users/steven/_CODE/DRUNK/DKNet/src/EfCore/DKNet.EfCore.Specifications/Dynamics/DynamicPredicateBuilderExtensions.cs`

**Changes**:
- Add cases for `In` and `NotIn` in the operation switch statement
- Generate System.Linq.Dynamic.Core syntax: `@0.Contains(PropertyName)` for `In`
- Generate System.Linq.Dynamic.Core syntax: `!@0.Contains(PropertyName)` for `NotIn`
- Ensure array is passed as `@0` parameter to `DynamicExpressionParser.ParseLambda`

**Implementation**:
```csharp
internal static string BuildClause(string prop, DynamicOperations op, object? val, int paramIndex)
{
    return val switch
    {
        null when op is DynamicOperations.Equal => $"{prop} == null",
        null when op is DynamicOperations.NotEqual => $"{prop} != null",
        _ => op switch
        {
            DynamicOperations.Equal => $"{prop} == @{paramIndex}",
            DynamicOperations.NotEqual => $"{prop} != @{paramIndex}",
            DynamicOperations.GreaterThan => $"{prop} > @{paramIndex}",
            DynamicOperations.GreaterThanOrEqual => $"{prop} >= @{paramIndex}",
            DynamicOperations.LessThan => $"{prop} < @{paramIndex}",
            DynamicOperations.LessThanOrEqual => $"{prop} <= @{paramIndex}",
            DynamicOperations.Contains => $"{prop}.Contains(@{paramIndex})",
            DynamicOperations.NotContains => $"!{prop}.Contains(@{paramIndex})",
            DynamicOperations.StartsWith => $"{prop}.StartsWith(@{paramIndex})",
            DynamicOperations.EndsWith => $"{prop}.EndsWith(@{paramIndex})",
            DynamicOperations.In => $"@{paramIndex}.Contains({prop})",        // Array contains property value
            DynamicOperations.NotIn => $"!@{paramIndex}.Contains({prop})",    // Array does NOT contain property value
            _ => throw new NotSupportedException($"Operation {op} not supported.")
        }
    };
}
```

**Note**: The syntax `@0.Contains(PropertyName)` tells System.Linq.Dynamic.Core to generate an expression where the array parameter (`@0`) contains the property value, which EF Core translates to SQL `IN`.

### Step 4: Update `AdjustOperationForValueType` (No Changes Needed)

**File**: `/Users/steven/_CODE/DRUNK/DKNet/src/EfCore/DKNet.EfCore.Specifications/Dynamics/DynamicPredicateBuilderExtensions.cs`

**Analysis**: 
- `In` and `NotIn` operations should NOT be adjusted based on property type
- They work with all types (int, string, enum, DateTime, etc.)
- The validation happens at the value level (must be array), not property type level
- **No changes required** to this method

### Step 5: Enhance `BuildDynamicExpression` in `DynamicPredicateExtensions.cs`

**File**: `/Users/steven/_CODE/DRUNK/DKNet/src/EfCore/DKNet.EfCore.Specifications/Dynamics/DynamicPredicateExtensions.cs`

**Changes**:
- Add array validation before calling `BuildClause` for `In`/`NotIn` operations
- Return `null` if array validation fails (triggers null-safe fallback)
- Ensure only valid collections reach `DynamicExpressionParser.ParseLambda`

**Implementation**:
```csharp
private static Expression<Func<T, bool>>? BuildDynamicExpression<T>(string propertyName,
    DynamicOperations operation, object? value)
{
    // Normalize property path using PropertyNameExtensions (PascalCase each segment)
    var normalizedPath = propertyName.ToPascalCase();

    var propType = typeof(T).ResolvePropertyType(normalizedPath);
    if (propType == null)
        return null;

    // Validate array value for In/NotIn operations
    if (!DynamicPredicateBuilderExtensions.ValidateArrayValue(value, operation))
        return null;

    // Adjust operation for type (In/NotIn are not adjusted)
    var op = propType.AdjustOperationForValueType(operation);

    // Validate enum value if needed
    if (!propType.ValidateEnumValue(value))
        return null;

    // Build the dynamic LINQ predicate string using shared BuildClause method
    var predicateString = DynamicPredicateBuilderExtensions.BuildClause(normalizedPath, op, value, 0);

    try
    {
        // Use System.Linq.Dynamic.Core to parse the predicate string
        // For In/NotIn, value is the array passed as @0 parameter
        var lambda = value == null
            ? DynamicExpressionParser.ParseLambda<T, bool>(ParsingConfig.Default, false, predicateString)
            : DynamicExpressionParser.ParseLambda<T, bool>(ParsingConfig.Default, false, predicateString, value);

        return lambda;
    }
#pragma warning disable CA1031 // Do not catch general exception types - DynamicExpressionParser can throw various exception types
    catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
    {
        // If parsing fails, return null (invalid expression)
        return null;
    }
}
```

### Step 6: Write Comprehensive Unit Tests

**File**: `/Users/steven/_CODE/DRUNK/DKNet/src/EfCore/EfCore.Specifications.Tests/DynamicPredicateExtensionsAdvancedTests.cs`

**Test Cases**:

#### Test 1: In Operation with Int Array
```csharp
[Fact]
public async Task DynamicAnd_WithInOperation_IntArray_ReturnsMatchingRecords()
{
    // Arrange
    var categoryIds = new[] { 1, 2, 3 };
    var predicate = PredicateBuilder.New<Product>(true)
        .DynamicAnd("CategoryId", DynamicOperations.In, categoryIds);

    // Act
    var results = await _context.Products
        .AsExpandable()
        .Where(predicate)
        .ToListAsync();

    // Assert
    results.ShouldNotBeEmpty();
    results.ShouldAllBe(p => categoryIds.Contains(p.CategoryId));
    
    // Verify SQL contains IN clause
    var query = _context.Products.AsExpandable().Where(predicate);
    var sql = query.ToQueryString();
    sql.ShouldContain("IN"); // SQL Server IN clause
}
```

#### Test 2: NotIn Operation with String Array
```csharp
[Fact]
public async Task DynamicAnd_WithNotInOperation_StringArray_ExcludesRecords()
{
    // Arrange
    var excludedNames = new[] { "Product A", "Product B" };
    var predicate = PredicateBuilder.New<Product>(true)
        .DynamicAnd("Name", DynamicOperations.NotIn, excludedNames);

    // Act
    var results = await _context.Products
        .AsExpandable()
        .Where(predicate)
        .ToListAsync();

    // Assert
    results.ShouldNotBeEmpty();
    results.ShouldAllBe(p => !excludedNames.Contains(p.Name));
}
```

#### Test 3: In Operation with Enum Array
```csharp
[Fact]
public async Task DynamicAnd_WithInOperation_EnumArray_ReturnsMatchingRecords()
{
    // Arrange
    var statuses = new[] { OrderStatus.Pending, OrderStatus.Processing };
    var predicate = PredicateBuilder.New<Order>(true)
        .DynamicAnd("Status", DynamicOperations.In, statuses);

    // Act
    var results = await _context.Orders
        .AsExpandable()
        .Where(predicate)
        .ToListAsync();

    // Assert
    results.ShouldNotBeEmpty();
    results.ShouldAllBe(o => statuses.Contains(o.Status));
}
```

#### Test 4: In Operation with Enum Int Array
```csharp
[Fact]
public async Task DynamicAnd_WithInOperation_EnumIntArray_ReturnsMatchingRecords()
{
    // Arrange - Using int values for enum
    var statusValues = new[] { 0, 1 }; // Pending = 0, Processing = 1
    var predicate = PredicateBuilder.New<Order>(true)
        .DynamicAnd("Status", DynamicOperations.In, statusValues);

    // Act
    var results = await _context.Orders
        .AsExpandable()
        .Where(predicate)
        .ToListAsync();

    // Assert
    results.ShouldNotBeEmpty();
    results.ShouldAllBe(o => statusValues.Contains((int)o.Status));
}
```

#### Test 5: Empty Array Handling
```csharp
[Fact]
public void DynamicAnd_WithInOperation_EmptyArray_ReturnsOriginalPredicate()
{
    // Arrange
    var emptyArray = Array.Empty<int>();
    var predicate = PredicateBuilder.New<Product>(p => p.IsActive);

    // Act
    var result = predicate.DynamicAnd("CategoryId", DynamicOperations.In, emptyArray);

    // Assert
    // Empty array should be invalid, so original predicate is returned
    var query = _context.Products.AsExpandable().Where(result);
    var sql = query.ToQueryString();
    sql.ShouldNotContain("IN"); // Should not have IN clause
    sql.ShouldContain("[IsActive]"); // Should only have original condition
}
```

#### Test 6: Null Array Handling
```csharp
[Fact]
public void DynamicAnd_WithInOperation_NullArray_ReturnsOriginalPredicate()
{
    // Arrange
    var predicate = PredicateBuilder.New<Product>(p => p.IsActive);

    // Act
    var result = predicate.DynamicAnd("CategoryId", DynamicOperations.In, null);

    // Assert
    var query = _context.Products.AsExpandable().Where(result);
    var sql = query.ToQueryString();
    sql.ShouldNotContain("IN");
    sql.ShouldContain("[IsActive]");
}
```

#### Test 7: Single Value Array
```csharp
[Fact]
public async Task DynamicAnd_WithInOperation_SingleValueArray_ReturnsMatchingRecord()
{
    // Arrange
    var singleCategoryId = new[] { 1 };
    var predicate = PredicateBuilder.New<Product>(true)
        .DynamicAnd("CategoryId", DynamicOperations.In, singleCategoryId);

    // Act
    var results = await _context.Products
        .AsExpandable()
        .Where(predicate)
        .ToListAsync();

    // Assert
    results.ShouldAllBe(p => p.CategoryId == 1);
}
```

#### Test 8: List<T> Support
```csharp
[Fact]
public async Task DynamicAnd_WithInOperation_List_ReturnsMatchingRecords()
{
    // Arrange
    var categoryIdsList = new List<int> { 1, 2, 3 };
    var predicate = PredicateBuilder.New<Product>(true)
        .DynamicAnd("CategoryId", DynamicOperations.In, categoryIdsList);

    // Act
    var results = await _context.Products
        .AsExpandable()
        .Where(predicate)
        .ToListAsync();

    // Assert
    results.ShouldNotBeEmpty();
    results.ShouldAllBe(p => categoryIdsList.Contains(p.CategoryId));
}
```

#### Test 9: SQL Injection Prevention Test
```csharp
[Fact]
public async Task DynamicAnd_WithInOperation_MaliciousInput_SafelyParameterized()
{
    // Arrange - Attempt SQL injection via array values
    var maliciousValues = new[] { "Product'; DROP TABLE Products--", "Normal Product" };
    var predicate = PredicateBuilder.New<Product>(true)
        .DynamicAnd("Name", DynamicOperations.In, maliciousValues);

    // Act
    var results = await _context.Products
        .AsExpandable()
        .Where(predicate)
        .ToListAsync();

    // Assert
    // Should safely parameterize and treat as literal string value
    // Products table should still exist
    var allProducts = await _context.Products.ToListAsync();
    allProducts.ShouldNotBeEmpty(); // Table not dropped
    
    // Verify SQL uses parameters
    var query = _context.Products.AsExpandable().Where(predicate);
    var sql = query.ToQueryString();
    sql.ShouldNotContain("DROP TABLE"); // Malicious SQL not injected
}
```

#### Test 10: Chained In/NotIn Operations
```csharp
[Fact]
public async Task DynamicAnd_CombinedInAndNotIn_ReturnsCorrectRecords()
{
    // Arrange
    var includedCategories = new[] { 1, 2, 3 };
    var excludedNames = new[] { "Excluded Product" };
    
    var predicate = PredicateBuilder.New<Product>(true)
        .DynamicAnd("CategoryId", DynamicOperations.In, includedCategories)
        .DynamicAnd("Name", DynamicOperations.NotIn, excludedNames);

    // Act
    var results = await _context.Products
        .AsExpandable()
        .Where(predicate)
        .ToListAsync();

    // Assert
    results.ShouldAllBe(p => 
        includedCategories.Contains(p.CategoryId) && 
        !excludedNames.Contains(p.Name));
}
```

#### Test 11: String as Array (Should Fail Validation)
```csharp
[Fact]
public void DynamicAnd_WithInOperation_StringValue_ReturnsOriginalPredicate()
{
    // Arrange - String should NOT be treated as array even though it's IEnumerable<char>
    var stringValue = "test";
    var predicate = PredicateBuilder.New<Product>(p => p.IsActive);

    // Act
    var result = predicate.DynamicAnd("Name", DynamicOperations.In, stringValue);

    // Assert
    var query = _context.Products.AsExpandable().Where(result);
    var sql = query.ToQueryString();
    sql.ShouldNotContain("IN");
    sql.ShouldContain("[IsActive]");
}
```

### Step 7: Add Integration Test for SQL Generation

**File**: `/Users/steven/_CODE/DRUNK/DKNet/src/EfCore/EfCore.Specifications.Tests/DynamicPredicateExtensionsAdvancedTests.cs`

**Test Case**:
```csharp
[Fact]
public void DynamicAnd_WithInOperation_GeneratesParameterizedInClause()
{
    // Arrange
    var categoryIds = new[] { 1, 2, 3 };
    var predicate = PredicateBuilder.New<Product>(true)
        .DynamicAnd("CategoryId", DynamicOperations.In, categoryIds);

    // Act
    var query = _context.Products.AsExpandable().Where(predicate);
    var sql = query.ToQueryString();

    // Assert - Verify SQL is parameterized
    sql.ShouldContain("IN"); // Has IN clause
    sql.ShouldNotContain("IN (1, 2, 3)"); // NOT hard-coded values
    sql.ShouldMatch(@"IN\s*\(@__"); // Should have parameter like @__p_0
    
    // Verify it's a valid SQL query
    sql.ShouldContain("SELECT");
    sql.ShouldContain("FROM [Products]");
}
```

## Testing Strategy

### Unit Tests (DynamicPredicateBuilderExtensionsTests.cs)
1. Test `ValidateArrayValue` with various inputs:
   - Null value → returns `false`
   - Empty array → returns `false`
   - String value → returns `false`
   - Non-enumerable → returns `false`
   - Valid array → returns `true`
   - Valid List<T> → returns `true`

### Integration Tests (DynamicPredicateExtensionsAdvancedTests.cs)
1. Test SQL generation with TestContainers (real SQL Server)
2. Verify parameterization prevents SQL injection
3. Test with different collection types (array, List, IEnumerable)
4. Test with different data types (int, string, enum, DateTime)
5. Test edge cases (empty, null, single value)
6. Test chained operations (In + NotIn, In + other filters)

## Security Considerations

### SQL Injection Prevention
- **CRITICAL**: All expression building uses `DynamicExpressionParser.ParseLambda` with parameters
- Array values are passed as `@0` parameter, not concatenated into SQL string
- Malicious array values (e.g., `["value'; DROP TABLE--"]`) are safely parameterized
- No manual SQL string construction or concatenation

### Type Safety
- Array element types should match property type for optimal SQL generation
- Runtime type validation by `DynamicExpressionParser.ParseLambda`
- Invalid types return `null` expression (graceful degradation)

## Performance Considerations

### Large Arrays
- SQL Server has limits on IN clause size (typically 2000+ values is problematic)
- Consider documenting recommended max array size (e.g., 100-500 values)
- For very large sets, recommend alternative approaches (temp tables, table-valued parameters)
- **Decision**: Leave validation to caller, document in XML comments

### SQL Generation
- System.Linq.Dynamic.Core generates: `@0.Contains(PropertyName)`
- EF Core translates to: `WHERE PropertyName IN (@p0, @p1, @p2...)`
- Each array element becomes a separate parameter
- Performance is comparable to hand-written LINQ `Where(p => ids.Contains(p.Id))`

## Open Questions for Refinement

1. **Empty Array Behavior**: 
   - Current: Returns `null` expression (original predicate unchanged)
   - Alternative: Could generate `WHERE 1=0` for `In`, `WHERE 1=1` for `NotIn`
   - **Recommendation**: Keep current null-safe behavior for consistency

2. **Array Size Limits**:
   - Should we validate/warn for arrays > 1000 elements?
   - **Recommendation**: Document in XML comments, leave to caller

3. **String Array Case Sensitivity**:
   - Should `In` on string arrays be case-insensitive by default?
   - **Recommendation**: Follow SQL Server default (case-insensitive), add separate operation for case-sensitive later if needed

4. **IQueryable<T> Support**:
   - Should we support `IQueryable<int>` as value (subquery)?
   - **Recommendation**: Out of scope for now, document limitation

## Documentation Updates

### XML Documentation
- Update `DynamicOperations` enum with detailed In/NotIn documentation
- Document array requirement in method XML comments
- Add examples in XML `<example>` tags showing array usage
- Document performance considerations for large arrays

### README/Wiki
- Add section on In/NotIn operations with code examples
- Document SQL injection safety via `DynamicExpressionParser.ParseLambda`
- Show comparison with hand-written LINQ: `Where(p => ids.Contains(p.Id))`

## Success Criteria

- ✅ `In` and `NotIn` enum values added with XML documentation
- ✅ Array validation implemented and tested
- ✅ `BuildClause` supports In/NotIn with correct Dynamic LINQ syntax
- ✅ All expressions use `DynamicExpressionParser.ParseLambda` (no SQL injection)
- ✅ 11+ unit/integration tests passing with TestContainers
- ✅ SQL generation verified to produce parameterized IN clauses
- ✅ Zero compilation warnings (`TreatWarningsAsErrors=true`)
- ✅ Code coverage > 85% for new code
- ✅ SQL injection prevention test passes

## Implementation Order

1. Add enum values (simplest, establishes contract)
2. Add array validation helper (supports validation logic)
3. Update `BuildClause` (core functionality)
4. Update `BuildDynamicExpression` (integration point)
5. Write unit tests (validate each piece)
6. Write integration tests (validate end-to-end)
7. Add SQL injection test (security validation)
8. Update documentation (knowledge sharing)

---

**Total Estimated Effort**: 3-4 hours
**Risk Level**: Low (follows existing patterns, uses proven library)
**Dependencies**: System.Linq.Dynamic.Core (already in use)

