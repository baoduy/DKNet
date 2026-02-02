# DKNet Framework Memory Bank

Welcome to the DKNet Framework Memory Bank—the central knowledge repository for GitHub Copilot and development teams working on the DKNet Framework.

---

## 📖 What Is This?

The Memory Bank is a structured collection of Markdown documents that capture all project context, decisions, patterns, and progress. Because AI assistants like GitHub Copilot start fresh each session, the Memory Bank serves as **persistent memory** to maintain continuity, accuracy, and consistency across sessions.

---

## 🗂️ Memory Bank Structure

### Core Files (Required)

| File | Purpose | When to Read |
|------|---------|--------------|
| **[projectbrief.md](projectbrief.md)** | High-level project overview, goals, and scope | Every session start |
| **[productContext.md](productContext.md)** | Why the project exists, problems it solves, user value | Feature planning |
| **[systemPatterns.md](systemPatterns.md)** | Architecture patterns, design decisions, component relationships | Before implementing features |
| **[techContext.md](techContext.md)** | Technology stack, frameworks, dependencies, constraints | Setting up dev environment |
| **[activeContext.md](activeContext.md)** | Current work focus, recent changes, immediate priorities | Every session start |
| **[progress.md](progress.md)** | Completed work, current status, what's left to build | Status checks, planning |
| **[copilot-rules.md](copilot-rules.md)** | Project-specific rules, patterns, security policies | Code generation, reviews |

### Supporting Directories

| Directory | Purpose |
|-----------|---------|
| **[feature-template/](feature-template/)** | Templates for feature specs, designs, tasks, and context |

---

## 🚀 Quick Start

### For GitHub Copilot

**Before ANY work, load these files in this order:**

1. ✅ **projectbrief.md** - Understand project goals
2. ✅ **activeContext.md** - Know current focus
3. ✅ **techContext.md** - Technology constraints
4. ✅ **systemPatterns.md** - Design patterns
5. ✅ **copilot-rules.md** - Code standards
6. ✅ **progress.md** - Current status

### For Developers

**When starting work:**
- Read `activeContext.md` for current priorities
- Check `progress.md` for completed work
- Review `copilot-rules.md` for coding standards

**When planning features:**
- Review `systemPatterns.md` for design patterns
- Check `techContext.md` for technology constraints
- Use templates in `feature-template/` for consistency

**When updating context:**
- Update `activeContext.md` when switching focus areas
- Update `progress.md` after completing significant work
- Update `copilot-rules.md` when discovering new patterns

---

## 📚 DKNet Framework Overview

**DKNet Framework** is a comprehensive .NET 10+ library collection for building enterprise-grade applications using Domain-Driven Design (DDD) and Onion Architecture principles.

### Core Areas

#### 1. **Core Extensions** (`/Core`)
- **DKNet.Fw.Extensions** - Core utilities, type extensions, helpers
- **DKNet.RandomCreator** - Test data generation with Bogus integration

#### 2. **Entity Framework Core** (`/EfCore`)
- **DKNet.EfCore.Abstractions** - Base entities, interfaces, aggregate roots
- **DKNet.EfCore.Extensions** - EF Core query extensions and utilities
- **DKNet.EfCore.Specifications** - Specification pattern with dynamic LINQ
- **DKNet.EfCore.Repos** - Repository pattern implementation
- **DKNet.EfCore.Events** - Domain event handling
- **DKNet.EfCore.Hooks** - Entity lifecycle hooks
- **DKNet.EfCore.AuditLogs** - Change tracking and audit trails
- **DKNet.EfCore.Encryption** - Property-level encryption
- **DKNet.EfCore.DataAuthorization** - Row-level security
- **DKNet.EfCore.DtoGenerator** - DTO generation utilities

#### 3. **ASP.NET Core** (`/AspNet`)
- **DKNet.AspCore.Extensions** - ASP.NET Core utilities
- **DKNet.AspCore.Idempotency** - Request idempotency middleware
- **DKNet.AspCore.Idempotency.MsSqlStore** - SQL Server idempotency store
- **DKNet.AspCore.Tasks** - Background task management

#### 4. **Services** (`/Services`)
- **DKNet.Svc.BlobStorage.*** - Multi-provider blob storage (Azure, AWS S3, Local)
- **DKNet.Svc.Encryption** - Encryption services
- **DKNet.Svc.PdfGenerators** - PDF generation
- **DKNet.Svc.Transformation** - Data transformation utilities

#### 5. **Messaging** (`/SlimBus`)
- **DKNet.SlimBus.Extensions** - CQRS and messaging patterns

#### 6. **Aspire** (`/Aspire`)
- **Aspire.Hosting.ServiceBus** - .NET Aspire service bus hosting

---

## 🎯 Current Development Focus

**Active Area**: Idempotency Framework Enhancements

**Recent Completions**:
- ✅ HTTP response code caching
- ✅ Configuration validation
- ✅ Cache exception handling
- ✅ Key validation and composite key format
- ✅ Enhanced logging with RequestId tracking

**Next Steps**:
- Comprehensive unit test coverage
- Integration test expansion
- Performance benchmarking
- Documentation updates

See [activeContext.md](activeContext.md) and [progress.md](progress.md) for detailed status.

---

## 🏗️ Project Architecture

### Design Principles

1. **Domain-Driven Design (DDD)**
   - Rich domain models with business logic encapsulation
   - Aggregate roots, entities, value objects
   - Domain events for loose coupling

2. **Onion Architecture**
   - Clear separation of concerns
   - Dependency inversion (core has no dependencies)
   - Infrastructure at the edges

3. **Specification Pattern**
   - Reusable, composable query logic
   - Type-safe with LinqKit integration
   - Dynamic predicate building

4. **Repository Pattern**
   - Abstracted data access
   - Specification-based queries
   - Unit of work support

5. **CQRS (Command Query Responsibility Segregation)**
   - Separate command and query models
   - MediatR integration via SlimBus
   - Event-driven architecture

---

## 🛠️ Technology Stack

| Component | Version | Purpose |
|-----------|---------|---------|
| **.NET** | 10.0+ | Core framework |
| **C#** | 13 | Language version |
| **EF Core** | 10.0+ | ORM and data access |
| **xUnit** | Latest | Test framework |
| **Shouldly** | Latest | Fluent assertions |
| **TestContainers** | Latest | Integration testing with real databases |
| **Bogus** | Latest | Test data generation |
| **System.Linq.Dynamic.Core** | Latest | Dynamic LINQ queries |
| **LinqKit** | Latest | Expression tree composition |

---

## 📏 Code Quality Standards

### Non-Negotiable Requirements

- ✅ **Zero Warnings** - `TreatWarningsAsErrors=true` across all projects
- ✅ **Nullable Reference Types** - `<Nullable>enable</Nullable>` mandatory
- ✅ **XML Documentation** - All public APIs require comprehensive XML docs
- ✅ **File Headers** - Copyright and license headers on all source files
- ✅ **Async/Await** - All I/O operations must be async
- ✅ **Test Coverage** - 85%+ target coverage

### Naming Conventions

**Test Naming**: `MethodName_Scenario_ExpectedBehavior`
```csharp
[Fact]
public void DynamicAnd_WhenPropertyNotFound_ThrowsArgumentException()
```

**File Naming**: PascalCase matching the primary type
```
IdempotencyEndpointFilter.cs
DynamicPredicateBuilder.cs
```

**XML Documentation**: Required structure
```csharp
/// <summary>
///     Brief description of the method purpose.
/// </summary>
/// <typeparam name="T">Description of generic parameter.</typeparam>
/// <param name="paramName">Description of parameter.</param>
/// <returns>Description of return value.</returns>
/// <exception cref="ArgumentNullException">When paramName is null.</exception>
```

---

## 🧪 Testing Strategy

### Unit Tests
- **Framework**: xUnit with Shouldly assertions
- **Pattern**: Arrange-Act-Assert (AAA)
- **Naming**: `MethodName_Scenario_ExpectedBehavior`
- **Organization**: Semantic regions for related tests

### Integration Tests
- **Framework**: xUnit with TestContainers
- **Database**: Real SQL Server via TestContainers.MsSql
- **Benefits**: Catches SQL-specific issues, accurate behavior testing
- **Pattern**: `IAsyncLifetime` fixture with container lifecycle

### Test Coverage Goals
- **Target**: 85%+ overall coverage
- **Critical Paths**: 100% coverage required
- **Public APIs**: Full behavioral coverage

---

## 🔐 Security Guidelines

### Secrets Management
- ❌ **NEVER** commit secrets, API keys, connection strings
- ✅ Use `appsettings.Development.json` (gitignored)
- ✅ Use `.env.example` with placeholders for documentation
- ✅ Use Azure Key Vault / AWS Secrets Manager in production
- 🚨 If secret leaked: rotate immediately, purge git history, notify team

### Input Validation
- ✅ Validate all user input
- ✅ Sanitize for injection attacks
- ✅ Use parameterized queries (EF Core does this)
- ✅ Validate idempotency keys (length, format, pattern)

---

## 📖 How to Use This Memory Bank

### For Code Generation
1. Load all core files (see Quick Start above)
2. Check `copilot-rules.md` for project-specific patterns
3. Reference `systemPatterns.md` for architecture guidance
4. Follow examples in existing codebase
5. Validate with `get_errors` tool after edits

### For Feature Planning
1. Review `activeContext.md` for current priorities
2. Check `progress.md` for completed work
3. Use `feature-template/` for consistent specifications
4. Update `systemPatterns.md` if new patterns emerge
5. Update `copilot-rules.md` with new standards

### For Context Updates
1. **After major changes**: Update `progress.md`
2. **When switching focus**: Update `activeContext.md`
3. **New patterns discovered**: Update `copilot-rules.md` and `systemPatterns.md`
4. **User requests "update memory bank"**: Review ALL files

---

## 🔄 Maintenance

### When to Update

| Trigger | Update Files |
|---------|--------------|
| Complete major feature | `progress.md`, `activeContext.md` |
| Discover new pattern | `copilot-rules.md`, `systemPatterns.md` |
| Change priorities | `activeContext.md` |
| Add technology/dependency | `techContext.md` |
| Learn project-specific rule | `copilot-rules.md` |

### Review Checklist

When user requests **"update memory bank"**:
- [ ] Review `projectbrief.md` - Still accurate?
- [ ] Review `productContext.md` - Product vision unchanged?
- [ ] Review `systemPatterns.md` - New patterns to document?
- [ ] Review `techContext.md` - Dependencies up to date?
- [ ] Update `activeContext.md` - Current focus accurate?
- [ ] Update `progress.md` - Completed work documented?
- [ ] Review `copilot-rules.md` - New rules discovered?

---

## 🆘 Getting Help

### For Copilot Agents

If uncertain about:
- **Patterns** → Read `systemPatterns.md` and search existing code
- **Standards** → Check `copilot-rules.md` for rules
- **Current Work** → Read `activeContext.md`
- **History** → Check `progress.md`

### For Developers

- **Architecture Questions** → Review `systemPatterns.md`
- **Code Standards** → Check `copilot-rules.md`
- **Getting Started** → Read main [README.md](../README.md)
- **API Documentation** → Check package-specific READMEs in each project folder

---

## 📊 Project Status

**Framework Version**: .NET 10+  
**Language**: C# 13  
**Status**: Active Development  
**License**: MIT  
**Last Major Update**: February 2, 2026

**Key Metrics**:
- 📦 **Packages**: 20+ NuGet packages
- 🧪 **Test Projects**: 15+ comprehensive test suites
- 📈 **Coverage**: 85%+ target across framework
- ⚠️ **Warnings**: Zero tolerance policy
- 📖 **Documentation**: XML docs on all public APIs

---

## 🎓 Learning Path

**New to DKNet Framework?** Follow this path:

1. Read [main README.md](../README.md) for overview
2. Review `systemPatterns.md` for architecture understanding
3. Explore package-specific READMEs (e.g., `EfCore/DKNet.EfCore.Specifications/README.md`)
4. Study test projects for usage examples
5. Check `copilot-rules.md` for coding standards

**Contributing?** Also read:
- `techContext.md` for development setup
- `activeContext.md` for current priorities
- `progress.md` for status and roadmap

---

## 📝 Document Conventions

### File Naming
- All lowercase with hyphens: `system-patterns.md` (exception: `README.md`)
- No spaces, use hyphens for readability
- Descriptive names reflecting content purpose

### Formatting
- **Headers**: Use ATX-style headers (`#`, `##`, `###`)
- **Lists**: Use `-` for unordered, `1.` for ordered
- **Code**: Use triple backticks with language identifier
- **Links**: Relative paths within memory bank
- **Emojis**: Use for visual navigation and scanning

### Content Guidelines
- **Concise**: Information-dense, avoid fluff
- **Structured**: Use headers, lists, tables for scannability
- **Examples**: Include code examples where helpful
- **Updated**: Keep accurate, update when context changes

---

## 🚀 Next Steps

After reading this README:

1. ✅ Load core files in recommended order (see Quick Start)
2. ✅ Review [copilot-quick-reference.md](copilot-quick-reference.md) for code patterns
3. ✅ Check [activeContext.md](activeContext.md) for current focus
4. ✅ Read [progress.md](progress.md) for completed work
5. ✅ Start coding with full context! 🎉

---

**Version**: 1.0.0  
**Last Updated**: February 2, 2026  
**Maintained By**: DKNet Framework Team  

**Remember**: This Memory Bank is the bridge between sessions. Keep it current, accurate, and comprehensive—your future self (and Copilot) will thank you! 🙏
