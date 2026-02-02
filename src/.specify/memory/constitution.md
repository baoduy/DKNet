<!--
Sync Impact Report
==================
Version change: 1.0.0 → 1.1.0 (Deep analysis update)

Modified Principles:
- I. Zero Warnings Tolerance - Added specific analyzer references
- II. Test-First with Real Databases - Added TestContainers fixture pattern
- VI. Pattern Compliance - Expanded with concrete module patterns

Added Sections:
- Solution Architecture (new section with module breakdown)
- Module-Specific Standards (new section)

Removed Sections: None

Templates requiring updates:
- ✅ plan-template.md - Constitution Check section aligns with principles
- ✅ spec-template.md - Requirements align with mandatory testing/documentation
- ✅ tasks-template.md - Task phases support foundational/test-first approach

Deferred Items: None

Deep Analysis Performed: 2026-01-29
- Analyzed 40+ projects across 6 solution folders
- Identified 14 NuGet library packages
- Reviewed test patterns across 16 test projects
- Validated Directory.Build.props and Directory.Packages.props
-->

# DKNet Framework Constitution

## Core Principles

### I. Zero Warnings Tolerance (NON-NEGOTIABLE)

All code MUST compile with zero warnings. This is enforced via `TreatWarningsAsErrors=true` in `Directory.Build.props`.

**Rules**:
- Every build MUST pass with `-warnaserror` or equivalent (`TreatWarningsAsErrors=true`)
- Nullable reference types MUST be enabled (`<Nullable>enable</Nullable>`)
- All nullable warnings MUST be resolved, not suppressed without explicit justification
- Code formatting MUST pass `dotnet format` validation before merge

**Rationale**: Warnings become bugs. Zero tolerance ensures code quality at compile time and prevents technical debt accumulation.

### II. Test-First with Real Databases (NON-NEGOTIABLE)

Test-driven development is mandatory. Integration tests MUST use real SQL Server via TestContainers—InMemory database is prohibited for EF Core tests.

**Rules**:
- TDD cycle: Write test → Verify it fails → Implement → Verify it passes → Refactor
- Integration tests MUST use `TestContainers.MsSql` for realistic SQL Server behavior
- `InMemoryDatabase` is PROHIBITED for EF Core integration tests (catches SQL-specific issues)
- Test naming: `MethodName_Scenario_ExpectedBehavior`
- Test structure: Arrange-Act-Assert pattern mandatory
- Coverage targets: 90%+ critical paths, 85%+ business logic, 80%+ utilities

**Rationale**: InMemory providers miss SQL-specific behaviors (transactions, collations, query translation). Real databases catch production issues during development.

### III. Async-Everywhere

All I/O operations MUST be asynchronous. Synchronous database access or blocking on async code is prohibited.

**Rules**:
- All EF Core queries MUST use async variants (`ToListAsync`, `FirstOrDefaultAsync`, etc.)
- NEVER block on async: `.Result`, `.Wait()`, or `GetAwaiter().GetResult()` are prohibited
- `async void` is prohibited except for event handlers
- Use `ConfigureAwait(false)` in library code for non-UI context
- Methods performing I/O MUST have `Async` suffix and return `Task` or `Task<T>`

**Rationale**: Blocking on async causes deadlocks in ASP.NET Core. Async-everywhere ensures scalability and thread pool efficiency.

### IV. Documentation & API Contracts

All public APIs MUST have XML documentation. External consumers depend on clear contracts.

**Rules**:
- All public classes, methods, and properties MUST have `<summary>` XML documentation
- `<param>`, `<returns>`, and `<exception>` tags MUST be present where applicable
- File headers with copyright MUST be present in all `.cs` files (see `FILE_HEADER_TEMPLATE.md`)
- Breaking changes MUST be clearly marked in commit messages and PR descriptions
- Memory bank documentation MUST be updated when patterns change

**Rationale**: DKNet is a NuGet library collection. Clear documentation enables adoption and reduces support burden.

### V. Security & Null Safety

No secrets in code. Null safety enforced via language features and explicit handling.

**Rules**:
- Secrets, API keys, and connection strings MUST NOT be committed to source control
- Use configuration providers (`IConfiguration`) for sensitive values
- Nullable reference types MUST be enabled and respected
- Null checks MUST be explicit or use null-conditional operators (`?.`, `??`)
- `ArgumentNullException` MUST be thrown for invalid null arguments with `nameof()`
- NEVER catch exceptions without logging or re-throwing

**Rationale**: Security vulnerabilities and null reference exceptions are the most common production issues. Prevention at code level is cheaper than runtime fixes.

### VI. Pattern Compliance

Code MUST follow established DKNet patterns. Consistency enables maintainability.

**Rules**:
- Dynamic predicates MUST use `PredicateBuilder.New<T>()` with `.DynamicAnd()` / `.DynamicOr()`
- LinqKit queries MUST include `.AsExpandable()` before `.Where()` with dynamic predicates
- Specifications MUST inherit from `Specification<TEntity>` base class
- Extension methods MUST be in static classes within `/Extensions` folder
- Repository pattern MUST be used for data access abstraction
- EF Core queries MUST use `.AsNoTracking()` for read-only operations
- Filtering MUST happen on database (`.Where()` before `.ToListAsync()`)

**Rationale**: Consistent patterns reduce cognitive load, enable code reuse, and simplify code reviews.

## Quality Gates

Pre-merge requirements that MUST pass for any code change.

**Build Gate**:
- `dotnet build -c Release` with zero warnings
- `dotnet format --verify-no-changes` passes

**Test Gate**:
- `dotnet test` with all tests passing
- Coverage thresholds met (see Principle II)
- No new test failures introduced

**Documentation Gate**:
- XML docs present for all new public APIs
- Memory bank updated if patterns/context changed
- File headers present on new `.cs` files

**Review Gate**:
- At least one approval from module owner
- All review comments addressed
- Commit messages follow conventional format (`type(scope): description`)

## Development Workflow

Standard process for feature development in DKNet Framework.

**Branch Naming**: `###-feature-name` (e.g., `001-dynamic-predicate-enums`)

**Commit Format**:
```
<type>(<scope>): <subject>

<body>

<footer>
```
- Types: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `perf`
- Scopes: `specifications`, `repository`, `extensions`, `tests`, `docs`, `core`

**PR Requirements**:
1. Problem statement describing the issue solved
2. Summary of changes with key modifications
3. Test evidence (coverage report, test results)
4. Breaking changes clearly marked if applicable
5. Linked issues/tasks referenced

**Pre-PR Checklist**:
- [ ] `dotnet build` passes with zero warnings
- [ ] `dotnet test` passes with coverage maintained
- [ ] `dotnet format` applied
- [ ] XML docs added for public APIs
- [ ] Memory bank updated if needed

## Governance

**Constitution Authority**: This constitution supersedes all other development practices for DKNet Framework. Conflicts are resolved in favor of constitution principles.

**Amendment Process**:
1. Propose change via PR modifying this constitution
2. Document rationale for change
3. Require approval from project maintainer
4. Update dependent templates if principles change
5. Increment constitution version appropriately

**Versioning Policy**:
- MAJOR: Principle removal or backward-incompatible redefinition
- MINOR: New principle added or existing principle materially expanded
- PATCH: Clarifications, typo fixes, non-semantic refinements

**Compliance Review**: All PRs MUST verify compliance with constitution principles. Reviewers are empowered to block merges for constitution violations.

**Guidance Reference**: For detailed coding patterns and examples, see:
- `/memory-bank/copilot-rules.md` - Complete coding standards
- `/memory-bank/systemPatterns.md` - Architecture patterns
- `/memory-bank/copilot-quick-reference.md` - Common task templates
- `/AGENTS.md` - AI agent quick reference

## Solution Architecture

**Project**: DKNet Framework  
**SDK**: .NET 10.0 (global.json: `"version": "10.0.100"`)  
**Target Framework**: `net10.0`  
**Package Management**: Central Package Management via `Directory.Packages.props`

### Solution Folders (6 domains, 40+ projects)

| Folder | Purpose | Key Libraries |
|--------|---------|---------------|
| **Core** | Foundation utilities | `DKNet.Fw.Extensions`, `DKNet.RandomCreator` |
| **EfCore** | Entity Framework Core extensions | 13 libraries (see below) |
| **Services** | Cloud & utility services | Blob Storage (AWS/Azure/Local), PDF, Encryption, Transformation |
| **SlimBus** | Messaging integration | SlimMessageBus extensions, ASP.NET Core integration |
| **AspNet** | ASP.NET Core hosting | Background Tasks, health checks |
| **Aspire** | .NET Aspire integration | Service Bus hosting |

### EfCore Module Breakdown (Primary Focus)

| Library | Purpose | Key Classes |
|---------|---------|-------------|
| `DKNet.EfCore.Abstractions` | Core interfaces | `IEntity<TKey>`, entity base contracts |
| `DKNet.EfCore.Specifications` | Specification Pattern | `Specification<T>`, `DynamicPredicateExtensions`, `Ops` enum |
| `DKNet.EfCore.Repos` | Repository Pattern | `ReadRepository<T>`, `WriteRepository<T>`, `Repository<T>` |
| `DKNet.EfCore.Repos.Abstractions` | Repository interfaces | `IReadRepository<T>`, `IWriteRepository<T>` |
| `DKNet.EfCore.Extensions` | EF Core utilities | Query extensions, DbContext helpers |
| `DKNet.EfCore.Hooks` | Interceptor pattern | `IHook` interface, SaveChanges interception |
| `DKNet.EfCore.Events` | Domain events | Event publishing via EF Core |
| `DKNet.EfCore.AuditLogs` | Audit trail | `AuditLogEntry`, automatic change tracking |
| `DKNet.EfCore.Encryption` | Field encryption | Transparent data encryption |
| `DKNet.EfCore.DataAuthorization` | Row-level security | Data authorization filters |
| `DKNet.EfCore.DtoGenerator` | Source generator | Automatic DTO generation |
| `DKNet.EfCore.DtoEntities` | DTO base types | DTO entity contracts |
| `DKNet.EfCore.Relational.Helpers` | SQL helpers | Relational database utilities |

### Key Dependencies

| Category | Package | Version |
|----------|---------|---------|
| **EF Core** | `Microsoft.EntityFrameworkCore` | 10.0.2 |
| **Dynamic LINQ** | `System.Linq.Dynamic.Core` | 1.7.1 |
| **LinqKit** | `LinqKit.Microsoft.EntityFrameworkCore` | 10.0.9 |
| **Testing** | `Testcontainers.MsSql` | 4.10.0 |
| **Assertions** | `Shouldly` | 4.3.0 |
| **Fake Data** | `Bogus` | 35.6.5 |
| **Mapping** | `Mapster` | 7.4.0 |
| **Analyzers** | `Meziantou.Analyzer`, `SonarAnalyzer.CSharp` | Latest |

## Module-Specific Standards

### DKNet.EfCore.Specifications

**Dynamic Predicate Building**:
```csharp
// REQUIRED: Use PredicateBuilder.New<T>() as starting point
var predicate = PredicateBuilder.New<Product>(true);

// REQUIRED: Use .DynamicAnd() / .DynamicOr() for runtime conditions
predicate = predicate
    .DynamicAnd("PropertyName", Ops.Equal, value)
    .DynamicAnd("NestedProperty.Path", Ops.Contains, searchTerm);

// REQUIRED: Use .AsExpandable() before .Where() with LinqKit
var results = await _db.Products
    .AsExpandable()
    .Where(predicate)
    .ToListAsync();
```

**Ops Enum** (supported operations):
- Comparison: `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual`
- String: `Contains`, `NotContains`, `StartsWith`, `EndsWith`
- Collection: `In`, `NotIn`

**Specification Pattern**:
```csharp
// REQUIRED: Inherit from Specification<TEntity>
public class ActiveProductSpec : Specification<Product>
{
    public ActiveProductSpec() : base(p => p.IsActive) { }
}
```

### DKNet.EfCore.Repos

**Repository Usage**:
```csharp
// Read operations: Use IReadRepository<T>
public class ProductService(IReadRepository<Product> repo)
{
    public Task<Product?> GetByIdAsync(int id) => repo.FindAsync(id);
    public IQueryable<Product> Query() => repo.Query();
}
```

### Test Fixture Pattern (TestContainers)

**REQUIRED** pattern for EF Core integration tests:
```csharp
public class TestDbFixture : IAsyncLifetime
{
    private MsSqlContainer? _msSqlContainer;
    public TestDbContext? Db { get; private set; }

    public async Task InitializeAsync()
    {
        _msSqlContainer = new MsSqlBuilder()
            .WithPassword("YourStrong@Passw0rd")
            .Build();
        await _msSqlContainer.StartAsync();
        
        // Create DbContext with container connection string
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(_msSqlContainer.GetConnectionString())
            .Options;
        Db = new TestDbContext(options);
        await Db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (Db != null) await Db.DisposeAsync();
        if (_msSqlContainer != null) await _msSqlContainer.DisposeAsync();
    }
}
```

**Version**: 1.1.0 | **Ratified**: 2026-01-29 | **Last Amended**: 2026-01-29
