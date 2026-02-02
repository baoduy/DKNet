# Next Steps: Phase 2 Implementation

**Status**: Phase 1 Complete ✅  
**Next**: Phase 2 Implementation  
**Branch**: `001-mssql-idempotency-store`  
**Timeline**: 5-7 days

---

## 🚀 Getting Started with Phase 2

### Step 1: Review the Plan
Read the plan documents in order:

```bash
cd /Users/steven/_CODE/DRUNK/DKNet/src/.specify/features/001-mssql-idempotency-store/

# 1. Quick overview
cat README.md

# 2. What to build
cat spec.md

# 3. How to build it
cat plan.md

# 4. Why each decision
cat research.md

# 5. Database design
cat data-model.md

# 6. Setup instructions
cat quickstart.md

# 7. API contracts
cat contracts/setup-guide.md
```

### Step 2: Set Up Development Environment

1. **Check .NET 10 SDK**
   ```bash
   dotnet --version  # Should be 10.0.100+
   ```

2. **Check SQL Server**
   ```bash
   # Install if needed:
   # Docker: docker run -e 'ACCEPT_EULA=Y' -e 'SA_PASSWORD=YourPassword' -p 1433:1433 mcr.microsoft.com/mssql/server
   # Or use existing local/cloud SQL Server 2019+
   ```

3. **Install TestContainers** (already in nuget.config)
   ```bash
   # No manual action needed - TestContainers.MsSql will be installed via NuGet
   ```

### Step 3: Understand the Architecture

**Two New Projects:**
```
DKNet.EfCore.Idempotency          (Library - 500 LOC)
EfCore.Idempotency.Tests          (Tests - 1500 LOC)
```

**Key Files to Create:**
```
Data/
  ├── IdempotencyDbContext.cs
  ├── IdempotencyKey.cs
  ├── Configurations/
  │   └── IdempotencyKeyConfiguration.cs

Store/
  └── IdempotencySqlServerStore.cs

Extensions/
  └── ServiceCollectionExtensions.cs
```

### Step 4: Phase 2 Tasks (When Ready)

When you're ready to begin implementation:

```bash
cd /Users/steven/_CODE/DRUNK/DKNet/src

# Update agent context with new technology info
.specify/scripts/bash/update-agent-context.sh copilot
```

This will create `tasks.md` with detailed Phase 2 tasks:
- Create projects
- Implement entities
- Write tests
- Generate migrations
- Documentation

---

## 📋 Phase 2 Breakdown

### Day 1: Project Setup & DbContext
**Deliverable**: DbContext + Entity + Configuration

Tasks:
- [ ] Create `DKNet.EfCore.Idempotency` project
- [ ] Add project references (AspCore.Idempotency, EF Core)
- [ ] Create `IdempotencyKey.cs` entity
- [ ] Create `IdempotencyDbContext.cs` with primary constructor
- [ ] Create `IdempotencyKeyConfiguration.cs` (IEntityTypeConfiguration)
- [ ] Verify code compiles with zero warnings

Expected Code:
- IdempotencyDbContext (30 lines)
- IdempotencyKey (80 lines)
- IdempotencyKeyConfiguration (120 lines)

### Day 2: Store Implementation
**Deliverable**: IdempotencySqlServerStore + Service Registration

Tasks:
- [ ] Create `IdempotencySqlServerStore.cs`
- [ ] Implement `IsKeyProcessedAsync()` method
- [ ] Implement `MarkKeyAsProcessedAsync()` method
- [ ] Create `ServiceCollectionExtensions.cs`
- [ ] Create `IdempotencyMsSqlOptions.cs` configuration class
- [ ] Add helper methods (key sanitization, response conversion)

Expected Code:
- IdempotencySqlServerStore (250 lines)
- ServiceCollectionExtensions (50 lines)
- IdempotencyMsSqlOptions (40 lines)

### Day 3: Migrations & Data Access
**Deliverable**: EF Core Migrations + Query Helpers

Tasks:
- [ ] Create test project `EfCore.Idempotency.Tests`
- [ ] Create `IdempotencyDbFixture.cs` with TestContainers
- [ ] Generate initial migration: `InitialIdempotencySchema`
- [ ] Verify migration SQL matches data-model.md
- [ ] Create query helper service for monitoring/cleanup
- [ ] Test migration up/down

Expected Code:
- IdempotencyDbFixture (100 lines)
- Migration file (150 lines auto-generated)
- Query helpers (100 lines)

### Day 4-5: Testing
**Deliverable**: Comprehensive Test Suite (85%+ coverage)

Test Categories:
- [ ] **Store Tests** (5-6 tests)
  - IsKeyProcessedAsync - key exists
  - IsKeyProcessedAsync - key not found
  - IsKeyProcessedAsync - key expired
  - MarkKeyAsProcessedAsync - insert success
  - MarkKeyAsProcessedAsync - race condition
  - MarkKeyAsProcessedAsync - large body handling

- [ ] **Concurrent Tests** (3-4 tests)
  - Concurrent requests same key
  - Race condition handling
  - Unique constraint violation

- [ ] **Error Handling Tests** (4-5 tests)
  - Database connection failure
  - Fail-open mode
  - Fail-closed mode
  - Serialization errors
  - Key validation

- [ ] **Integration Tests** (2-3 tests)
  - Full request-response cycle
  - Idempotency filter integration
  - Middleware interaction

- [ ] **Migration Tests** (1-2 tests)
  - Schema creation
  - Index creation
  - Constraint validation

Expected Code:
- 15-20 test methods
- ~1500 lines of test code
- 85%+ coverage

### Day 6: Documentation & Examples
**Deliverable**: README + Examples

Tasks:
- [ ] Create README.md for library
- [ ] Add inline code comments
- [ ] Create usage examples file
- [ ] Update main project documentation
- [ ] Create troubleshooting guide

### Day 7: Code Review & Polish
**Deliverable**: Production-Ready Code

Tasks:
- [ ] Verify zero warnings: `dotnet build -c Release`
- [ ] Run full test suite: `dotnet test`
- [ ] Check test coverage: `dotnet test /p:CollectCoverage=true`
- [ ] Code review with team
- [ ] Fix any issues
- [ ] Final verification

---

## 🧪 Testing Strategy

### Test Infrastructure
```csharp
// Fixture pattern (IAsyncLifetime)
[CollectionDefinition("IdempotencyDb Collection")]
public sealed class IdempotencyDbCollection : 
    ICollectionFixture<IdempotencyDbFixture> { }

// Test class
[Collection("IdempotencyDb Collection")]
public sealed class IdempotencySqlServerStoreTests
{
    private readonly IdempotencyDbFixture _fixture;
    
    [Fact]
    public async Task IsKeyProcessedAsync_WhenKeyNotExists_ReturnsFalseAndNull()
    {
        // Arrange
        var store = new IdempotencySqlServerStore(
            _fixture.DbContext,
            Options.Create(new IdempotencyMsSqlOptions()),
            _fixture.Logger);

        // Act
        var (processed, response) = await store.IsKeyProcessedAsync("nonexistent");

        // Assert
        processed.ShouldBeFalse();
        response.ShouldBeNull();
    }
}
```

### Test Data Generation
Use Bogus for realistic test data:
```csharp
var faker = new Faker<IdempotencyKey>()
    .RuleFor(k => k.Key, f => f.Random.Uuid().ToString())
    .RuleFor(k => k.Route, f => $"/api/{f.Internet.Slug()}")
    .RuleFor(k => k.HttpMethod, f => f.PickRandom("GET", "POST", "PUT", "DELETE"))
    .RuleFor(k => k.StatusCode, f => f.Random.Int(200, 599));
```

---

## 🔄 Implementation Patterns

### Pattern 1: Key Lookup
```csharp
var existing = await _context.IdempotencyKeys
    .AsNoTracking()
    .FirstOrDefaultAsync(k => 
        k.Key == sanitizedKey &&
        k.Route == route &&
        k.HttpMethod == method &&
        k.ExpiresAt > DateTime.UtcNow)
    .ConfigureAwait(false);

if (existing != null)
    return (true, ConvertToResponse(existing));
```

### Pattern 2: Store with Race Condition Handling
```csharp
try
{
    _context.IdempotencyKeys.Add(entity);
    await _context.SaveChangesAsync().ConfigureAwait(false);
}
catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
{
    _logger.LogDebug("Key already processed (expected race condition)");
    // Expected in concurrent scenarios - another request won
}
```

### Pattern 3: Cleanup Query
```csharp
var deletedCount = await _context.IdempotencyKeys
    .Where(k => k.ExpiresAt < DateTime.UtcNow)
    .ExecuteDeleteAsync()
    .ConfigureAwait(false);
```

---

## 📝 Code Quality Checklist

Before submitting for code review:

### Compilation
- [ ] `dotnet build -c Release` with zero warnings
- [ ] `dotnet format --verify-no-changes` passes
- [ ] All files have copyright header

### Testing
- [ ] `dotnet test` - all tests passing
- [ ] Code coverage ≥ 85% on critical paths
- [ ] No flaky tests
- [ ] Tests use real SQL Server (TestContainers)

### Documentation
- [ ] XML docs on all public APIs
- [ ] README.md complete
- [ ] Code comments on complex logic
- [ ] Examples included for common usage
- [ ] Troubleshooting guide written

### Security
- [ ] No hardcoded secrets
- [ ] No SQL injection vectors
- [ ] Key sanitization validated
- [ ] No sensitive data in logs

### Performance
- [ ] Query performance tested
- [ ] No N+1 queries
- [ ] Indexes utilized
- [ ] Connection pooling configured

### Pattern Compliance
- [ ] Follows DKNet conventions
- [ ] IIdempotencyKeyStore implemented correctly
- [ ] IEntityTypeConfiguration pattern used
- [ ] Async/await throughout
- [ ] No blocking calls

---

## 🐛 Debugging Tips

### TestContainers Not Starting
```bash
# Check Docker
docker ps

# Check logs
docker logs container_name

# Restart
docker restart container_name
```

### Migration Fails
```bash
# Check current migrations
dotnet ef migrations list

# Revert to previous
dotnet ef database update PreviousMigration

# Remove last migration
dotnet ef migrations remove
```

### Tests Failing
```bash
# Run single test with output
dotnet test --filter "TestName" -v detailed

# Check database state
# Use SQL Server Management Studio to inspect IdempotencyKeys table

# Run with specific logger level
dotnet test --logger "console;verbosity=detailed"
```

### Build Warnings
```bash
# See all warnings with details
dotnet build -c Release /p:TreatWarningsAsErrors=false

# Fix one by one
# Check specific warning numbers in build output
```

---

## 📚 Reference Documents

Keep these open while coding:

1. **data-model.md** - For entity field definitions and constraints
2. **quickstart.md** - For setup and configuration examples
3. **contracts/setup-guide.md** - For API contracts
4. **research.md** - For design rationale and alternatives

---

## 🎯 Success Criteria (Phase 2)

✅ Implementation complete:
- [ ] All 7 user stories working
- [ ] Service registration in DI container
- [ ] DbContext using EF Core 10 patterns
- [ ] Store implementation handling all scenarios
- [ ] Migrations generating correct SQL
- [ ] 85%+ test coverage achieved
- [ ] Zero compiler warnings
- [ ] All tests passing
- [ ] Documentation complete
- [ ] Code review approved

---

## 💡 Tips for Success

1. **Follow the plan documents closely** - They contain all the details you need
2. **Test first** - Write test before implementation (TDD approach)
3. **Use real database** - TestContainers prevents surprises in production
4. **Commit frequently** - Small, focused commits are easier to review
5. **Keep documentation updated** - Update as you learn new things
6. **Ask questions** - Refer to research.md for design rationale
7. **Celebrate milestones** - Each day completed is progress!

---

## 📞 Questions During Implementation?

Refer to these documents:

| Question | Document |
|----------|----------|
| Why this design? | research.md |
| How does schema work? | data-model.md |
| How do I set it up? | quickstart.md |
| What APIs are available? | contracts/setup-guide.md |
| What's the request flow? | data-model.md (data lifecycle) |
| How do I test it? | quickstart.md (test section) |
| How do I handle errors? | research.md (error handling) |

---

## 🚀 Ready to Begin?

When you're ready to start Phase 2:

1. ✅ Read the plan documents
2. ✅ Set up development environment
3. ✅ Run: `.specify/scripts/bash/update-agent-context.sh copilot`
4. ✅ Execute Phase 2 implementation tasks (from tasks.md)
5. ✅ Follow the daily breakdown above
6. ✅ Code, test, review, repeat!

---

**Good luck! You've got a solid plan. Let's build it! 🚀**

---

Generated: January 30, 2026  
Phase: 1 Complete → Phase 2 Next  
Framework: DKNet (.NET 10 | EF Core 10.0.2)
