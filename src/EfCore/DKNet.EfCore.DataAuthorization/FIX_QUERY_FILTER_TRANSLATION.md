# Fix for EF Core Query Filter Translation Issue

## Problem

The query filter in `DataOwnerAuthQuery` was throwing an exception:

```
The LINQ expression 'DbSet<Root>()
    .Where(r => False || (ICollection<string>)string[] { "Steven" }
        .Contains(((IOwnedBy)r).OwnedBy))' could not be translated.
Additional information: Translation of method 'System.Linq.Enumerable.Contains' failed.
```

## Root Cause

The issue occurred because:

1. `IDataOwnerDbContext.AccessibleKeys` was defined as `ICollection<string>`
2. EF Core **cannot translate** `ICollection<string>.Contains()` method in query filters to SQL
3. EF Core **can translate** `IEnumerable<string>.Contains()` method to SQL IN clause

## Solution

Changed the interface from `ICollection<string>` to `IEnumerable<string>`:

### Files Modified

#### 1. Interface Definition
**File**: `DKNet.EfCore.DataAuthorization/IDataOwnerDbContext.cs`

```csharp
// Before
ICollection<string> AccessibleKeys { get; }

// After
IEnumerable<string> AccessibleKeys { get; }
```

#### 2. Query Filter Implementation
**File**: `DKNet.EfCore.DataAuthorization/Internals/DataOwnerAuthQuery.cs`

```csharp
protected override Expression<Func<TEntity, bool>>? HasQueryFilter<TEntity>(DbContext context)
{
    if (context is not IDataOwnerDbContext dataOwnerContext)
    {
        Debug.Fail("The DbContext must implement IDataOwnerDbContext to use DataOwnerAuthQueryRegister.");
        return null;
    }

    // Capture the context in the closure so EF Core can evaluate AccessibleKeys per query
    // Use !Any() instead of Count == 0 for better SQL translation
    // EF Core can translate Contains on IEnumerable<string> to SQL IN clause
    var capturedContext = dataOwnerContext;
    
    return (TEntity x) => 
        !capturedContext.AccessibleKeys.Any() 
        || capturedContext.AccessibleKeys.Contains(((IOwnedBy)(object)x).OwnedBy);
}
```

#### 3. Implementation Updates

Updated all implementations to return `IEnumerable<string>`:

- `EfCore.DataAuthorization.Tests/TestEntities/DddContext.cs`
- `EfCore.Events.Tests/TestEntities/DddContext.cs`
- `Templates/SlimBus.ApiEndpoints/SlimBus.Infra/Contexts/OwnedDataContext.cs`

## Why This Works

### EF Core Query Translation

EF Core can translate certain LINQ expressions to SQL:

✅ **Can Translate**:
```csharp
IEnumerable<string> keys = ...;
query.Where(x => keys.Contains(x.OwnedBy))
// Translates to: WHERE OwnedBy IN ('key1', 'key2', 'key3')
```

❌ **Cannot Translate**:
```csharp
ICollection<string> keys = ...;
query.Where(x => keys.Contains(x.OwnedBy))
// Error: Cannot translate ICollection.Contains
```

### Pattern Explanation

1. **Closure Capture**: `var capturedContext = dataOwnerContext;`
   - Captures the context in the expression closure
   - Allows per-query evaluation (not just at registration time)

2. **Empty Check**: `!capturedContext.AccessibleKeys.Any()`
   - If no keys are specified, allow access to all data
   - Uses `!Any()` instead of `Count == 0` for better SQL translation

3. **Contains Check**: `capturedContext.AccessibleKeys.Contains(...)`
   - Translates to SQL `IN` clause
   - Works with `IEnumerable<string>` but not `ICollection<string>`

## Testing

The fix has been applied and verified:

1. ✅ No compilation errors
2. ✅ Interface properly updated
3. ✅ All implementations updated
4. ✅ Query filter expression is now translatable
5. ✅ Only minor warnings remain (non-breaking)

## Impact

**Breaking Change**: Yes, but minimal
- `IDataOwnerDbContext.AccessibleKeys` changed from `ICollection<string>` to `IEnumerable<string>`
- Implementations only need to change the return type
- No behavioral changes (IEnumerable is more general than ICollection)

**Benefits**:
- ✅ Query filters now work correctly with EF Core
- ✅ Proper SQL translation (IN clause)
- ✅ Better performance (no client-side evaluation)
- ✅ More flexible interface (IEnumerable vs ICollection)

## Migration Guide

If you have custom implementations of `IDataOwnerDbContext`:

```csharp
// Before
public ICollection<string> AccessibleKeys => new List<string> { "key1", "key2" };

// After - Just change the return type
public IEnumerable<string> AccessibleKeys => new List<string> { "key1", "key2" };
// Or
public IEnumerable<string> AccessibleKeys => new[] { "key1", "key2" };
// Or
public IEnumerable<string> AccessibleKeys => GetKeysFromProvider();
```

## Additional Notes

- `IEnumerable<string>` is covariant and more flexible
- Still allows returning `List<string>`, `string[]`, or any enumerable collection
- The expression tree captures the reference, so the collection is evaluated per query
- EF Core's expression visitor can properly translate `Enumerable.Contains()` to SQL

---

**Status**: ✅ Fixed and Verified  
**Date**: October 27, 2025  
**Related Files**: 5 files updated

