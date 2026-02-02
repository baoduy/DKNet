# DKNet.AspCore.Idempotency - Quick Action Checklist

**Generated**: January 30, 2026  
**Project**: DKNet.AspCore.Idempotency v10.0+  
**Overall Score**: 8.2/10

---

## 🚨 CRITICAL - DO FIRST (1 hour total)

### ✅ Task 1: Fix InternalsVisibleTo Typo
**Time**: 1 minute  
**File**: `DKNet.AspCore.Idempotency.csproj` line 16  
**Action**:
```diff
- <InternalsVisibleTo Include="AspCore.Idempotents.Tests"/>
+ <InternalsVisibleTo Include="AspCore.Idempotency.Tests"/>
```
**Blocker For**: Test implementation  
**Status**: [ ] TODO

---

### ✅ Task 2: Add Cache Exception Handling
**Time**: 2 hours  
**Files**: 
- `IIdempotencyKeyRepository.cs` - `IsKeyProcessedAsync()`
- `IIdempotencyKeyRepository.cs` - `MarkKeyAsProcessedAsync()`
- `IdempotencyEndpointFilter.cs` - `InvokeAsync()`

**Action**: Wrap cache calls in try-catch, log and fail-open:
```csharp
try 
{
    result = await cache.GetStringAsync(cacheKey).ConfigureAwait(false);
}
catch (OperationCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
{
    logger.LogWarning(ex, "Cache operation timeout, treating as cache miss");
    return (false, null);
}
catch (Exception ex)
{
    logger.LogError(ex, "Cache error, allowing request to proceed");
    return (false, null);
}
```
**Impact**: API stays up even if Redis is down  
**Status**: [ ] TODO

---

## 🟠 HIGH PRIORITY - SPRINT 1 (8-10 hours)

### ✅ Task 3: Add Idempotency Key Validation
**Time**: 1.5 hours  
**File**: `IdempotencyEndpointFilter.cs` lines 52-58  
**Action**: After checking if header is present, validate:
- Length (max 256 chars default)
- Pattern (regex match)
- Character set

**Code Template**:
```csharp
public class IdempotencyOptions
{
    public int MaxIdempotencyKeyLength { get; set; } = 256;
    public string? IdempotencyKeyPattern { get; set; } = @"^[a-zA-Z0-9\-_.]+$";
}

// In filter:
if (idempotencyKey.Length > options.MaxIdempotencyKeyLength)
    return Results.Problem(
        "Idempotency key exceeds maximum length",
        statusCode: StatusCodes.Status400BadRequest);

if (options.IdempotencyKeyPattern != null && 
    !Regex.IsMatch(idempotencyKey, options.IdempotencyKeyPattern))
    return Results.Problem(
        "Invalid idempotency key format",
        statusCode: StatusCodes.Status400BadRequest);
```
**Status**: [ ] TODO

---

### ✅ Task 4: Fix Composite Key Generation
**Time**: 1 hour  
**File**: `IdempotencyEndpointFilter.cs` lines 63-65  
**Action**: Include HTTP method in composite key:

**Current** (❌):
```csharp
var compositeKey = $"{routeTemplate}_{idempotencyKey}";
```

**Fixed** (✅):
```csharp
var httpMethod = context.HttpContext.Request.Method;
var compositeKey = $"{httpMethod}:{routeTemplate}_{idempotencyKey}".ToUpperInvariant();
```

**Why**: Ensure same key is unique per HTTP method (POST vs DELETE)  
**Status**: [ ] TODO

---

### ✅ Task 5: Add Response Serialization Error Handling
**Time**: 1.5 hours  
**File**: `IdempotencyEndpointFilter.cs` around line 107  
**Action**: Wrap JSON serialization in try-catch:

**Current** (❌):
```csharp
await cacher.MarkKeyAsProcessedAsync(compositeKey,
    JsonSerializer.Serialize(resultValue, _options.JsonSerializerOptions));
```

**Fixed** (✅):
```csharp
var serializedResult = null;
try 
{
    // Skip serialization for streaming responses
    if (resultValue is IAsyncEnumerable or Stream)
    {
        logger.LogWarning("Cannot cache streamed response for key {Key}", idempotencyKey);
    }
    else
    {
        serializedResult = JsonSerializer.Serialize(resultValue, _options.JsonSerializerOptions);
    }
}
catch (JsonException ex)
{
    logger.LogWarning(ex, "Failed to serialize response for idempotency caching");
}

if (serializedResult != null)
{
    await cacher.MarkKeyAsProcessedAsync(compositeKey, serializedResult).ConfigureAwait(false);
}
else
{
    // Mark as processed but don't cache result
    await cacher.MarkKeyAsProcessedAsync(compositeKey).ConfigureAwait(false);
}
```

**Impact**: Handle all response types gracefully  
**Status**: [ ] TODO

---

### ✅ Task 6: Make Status Code Range Configurable
**Time**: 1.5 hours  
**Files**: 
- `IdempotencyOptions.cs` - Add properties
- `IdempotencyEndpointFilter.cs` - Use properties

**Action**:
```csharp
// In IdempotencyOptions.cs
public int CacheableStatusCodeMin { get; set; } = 200;
public int CacheableStatusCodeMax { get; set; } = 299;
public HashSet<int> AdditionalCacheableStatusCodes { get; set; } = new() { 201, 202 };

// In IdempotencyEndpointFilter.cs line 100
var isCacheable = (statusCode >= _options.CacheableStatusCodeMin && 
                   statusCode <= _options.CacheableStatusCodeMax) ||
                  _options.AdditionalCacheableStatusCodes.Contains(statusCode);

if (isCacheable)
{
    // Cache the response
}
```

**Status**: [ ] TODO

---

### ✅ Task 7: Add Configuration Validation
**Time**: 1.5 hours  
**File**: `IdempotentSetup.cs` in `AddIdempotency()` method  
**Action**: Validate options on registration:

```csharp
public static IServiceCollection AddIdempotency(this IServiceCollection services,
    Action<IdempotencyOptions>? config = null)
{
    var options = new IdempotencyOptions();
    config?.Invoke(options);

    // Validate
    if (string.IsNullOrWhiteSpace(options.IdempotencyHeaderKey))
        throw new ArgumentException("IdempotencyHeaderKey cannot be empty");
    
    if (options.Expiration <= TimeSpan.Zero)
        throw new ArgumentException("Expiration must be positive");
    
    if (options.JsonSerializerOptions == null)
        throw new ArgumentException("JsonSerializerOptions cannot be null");
    
    // ... rest of registration
}
```

**Status**: [ ] TODO

---

### ✅ Task 8: Enhance Production Logging
**Time**: 2 hours  
**File**: `IdempotencyEndpointFilter.cs`  
**Action**: Add structured logging metrics:

```csharp
// At Info level (visible in production)
logger.LogInformation(
    "Idempotency: Key={IdempotencyKey}, CacheHit={CacheHit}, " +
    "Status={StatusCode}, Duration={Duration}ms",
    idempotencyKey, existingResult.processed, statusCode, sw.ElapsedMilliseconds);

// For conflicts, log the strategy used
logger.LogInformation("Idempotency conflict handled with strategy {Strategy}",
    _options.ConflictHandling);
```

**Metrics to Track**:
- Cache hits vs misses
- Conflict resolution actions
- Error rates
- Operation timing

**Status**: [ ] TODO

---

## 🟡 MEDIUM PRIORITY - SPRINT 2 (8-10 hours)

### ✅ Task 9: Fix Race Condition Vulnerability
**Time**: 3 hours  
**File**: `IdempotencyEndpointFilter.cs` - refactor logic  
**Complexity**: HIGH  
**Options**:
1. Use distributed lock (Redis SETNX)
2. Use SET with NX/EX (atomic compare-and-set)
3. Document as known limitation and recommend client retry strategy

**Recommended**: Option 2 - Atomic cache operation

**Status**: [ ] TODO (Defer to v10.2 if needed)

---

### ✅ Task 10: Standardize Naming Conventions
**Time**: 2 hours  
**Files**: All source files  
**Action**: Review and standardize:
- Class naming (consistency with prefix)
- Method naming (verb-based, consistent)
- Property naming (PascalCase)
- Parameter naming (camelCase)

**Checklist**:
- [ ] All classes follow naming convention
- [ ] All methods follow naming convention
- [ ] All properties follow naming convention
- [ ] No inconsistencies between related items

**Status**: [ ] TODO

---

### ✅ Task 11: Add Security Considerations to README
**Time**: 1.5 hours  
**File**: `README.md`  
**Action**: Add new section "Security Considerations":
- Cache key injection (explain sanitization)
- Memory attacks (DoS via large keys)
- Serialization safety
- Cache poisoning scenarios
- Recommended patterns

**Status**: [ ] TODO

---

## 🟦 LOW PRIORITY - FUTURE RELEASES

### Nice-to-Have Items (4-5 hours)
- [ ] Distributed tracing with System.Diagnostics.Activity
- [ ] Health check probe for cache connectivity
- [ ] IOptions<IdempotencyOptions> validation with FluentValidation
- [ ] TypedResults helpers for cleaner integration
- [ ] Performance benchmarks and metrics

**Status**: Defer to v10.2+

---

## 🧪 TEST IMPLEMENTATION (Priority)

### ✅ Task 12: Implement Core Test Suite
**Time**: 13 hours  
**Location**: `AspCore.Idempotency.Tests/`  
**Coverage Target**: 85%+

**Test Categories** (Priority Order):

#### P0 - Critical (Must Have)
1. **IdempotencyKeyValidationTests** (3 hours)
   - [ ] Valid UUID accepted
   - [ ] Empty key rejected
   - [ ] Key too long rejected
   - [ ] Invalid characters rejected

2. **DuplicateRequestHandlingTests** (3 hours)
   - [ ] Duplicate request with same key
   - [ ] Different key allowed
   - [ ] Key expires and new request allowed

3. **ConflictHandlingTests** (2 hours)
   - [ ] ConflictResponse strategy returns 409
   - [ ] CachedResult strategy returns cached response

4. **CacheOperationTests** (2 hours)
   - [ ] Successful response cached
   - [ ] Error response not cached
   - [ ] Cache entry expires

5. **ErrorHandlingTests** (2 hours)
   - [ ] Cache unavailable → request proceeds
   - [ ] Serialization fails gracefully
   - [ ] Invalid configuration detected

6. **IntegrationTests** (1 hour)
   - [ ] End-to-end POST with idempotency
   - [ ] End-to-end PUT with idempotency
   - [ ] End-to-end DELETE with idempotency

**Status**: [ ] TODO

#### P1 - Important (Should Have)
- [ ] ConcurrencyTests (race condition scenarios)
- [ ] CacheProviderCompatibilityTests (Redis, SQL, Memory)
- [ ] PerformanceBenchmarks

---

## 📊 Progress Tracking

### Week 1 - Critical Fixes
- [ ] Task 1: InternalsVisibleTo fix (1 min)
- [ ] Task 2: Cache exception handling (2 hrs)
- [ ] Task 3: Key validation (1.5 hrs)
- [ ] Task 4: Composite key fix (1 hr)
- [ ] Task 5: Response serialization (1.5 hrs)

**Week 1 Total**: 6+ hours  
**Target Date**: _________

---

### Week 2 - High Priority
- [ ] Task 6: Status code config (1.5 hrs)
- [ ] Task 7: Configuration validation (1.5 hrs)
- [ ] Task 8: Enhanced logging (2 hrs)
- [ ] Task 9: Race condition (3 hrs - optional)

**Week 2 Total**: 8+ hours  
**Target Date**: _________

---

### Weeks 3-4 - Test Implementation
- [ ] Task 12: Core test suite (13 hrs)

**Weeks 3-4 Total**: 13+ hours  
**Target Date**: _________

---

### Week 5 - Polish & Documentation
- [ ] Task 10: Naming standardization (2 hrs)
- [ ] Task 11: Security documentation (1.5 hrs)
- [ ] README updates (1.5 hrs)
- [ ] Final testing & QA (2 hrs)

**Week 5 Total**: 7+ hours  
**Target Date**: _________

---

## 📚 Reference Documents

For detailed information, see:
- **IDEMPOTENCY_SUMMARY.md** - Executive summary with roadmap
- **IDEMPOTENCY_ANALYSIS.md** - Comprehensive findings
- **IDEMPOTENCY_FIXES.md** - Code implementation examples
- **IDEMPOTENCY_TESTS.md** - Test implementations
- **IDEMPOTENCY_ANALYSIS_INDEX.md** - Navigation guide

---

## ✅ Release Criteria for v10.1

Before releasing v10.1, ensure:
- [ ] All critical issues resolved
- [ ] All high-priority issues resolved
- [ ] Test coverage ≥ 85%
- [ ] Zero compiler warnings
- [ ] README updated with fixes
- [ ] CHANGELOG entry added
- [ ] Version bumped in .csproj files

**Release Date Target**: _________

---

## 📝 Notes

**Assigned To**: _________  
**Last Updated**: January 30, 2026  
**Next Review**: _________

---

## Quick Links to Files

```
Critical Fixes:
- DKNet.AspCore.Idempotency.csproj (line 16)
- IIdempotencyKeyRepository.cs (exception handling)
- IdempotencyEndpointFilter.cs (validation, keys, serialization)

Configuration:
- IdempotencyOptions.cs (add properties)
- IdempotentSetup.cs (validation)

Tests:
- AspCore.Idempotency.Tests/ (implementation)
```

---

**Analysis Version**: 1.0  
**Status**: Ready for Implementation  
**Quality Gate**: PASS (8.2/10)
