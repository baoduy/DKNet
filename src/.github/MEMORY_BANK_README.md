# DKNet Framework - Copilot Memory Bank Guide

## 📋 Overview

The **Copilot Memory Bank** (`copilot-memory-bank.md`) is a comprehensive knowledge base designed to help GitHub Copilot understand the DKNet framework's architecture, patterns, and conventions. This document significantly improves Copilot's ability to provide accurate, context-aware suggestions that align with the project's standards.

## 🎯 Purpose

This memory bank serves as a "knowledge injection" for AI assistants, enabling them to:

1. **Understand the architecture** - DDD, Onion Architecture, CQRS patterns
2. **Follow established conventions** - Naming, code style, file organization
3. **Use correct patterns** - Repository pattern, entity design, CQRS with SlimBus
4. **Avoid anti-patterns** - Common mistakes and what to do instead
5. **Generate consistent code** - Matching the project's style and standards

## 📚 What's Included

### Core Documentation (1000+ lines)

#### 1. **Project Overview**
- Architecture principles (DDD, Onion, CQRS)
- Framework information
- Project structure
- All 20+ packages documented

#### 2. **Project Structure Deep Dive**
- **EfCore Projects**: All 13 packages with features and usage
- **SlimBus Projects**: CQRS implementation details
- **Service Projects**: Blob storage, encryption, PDF, etc.
- **Background Tasks**: Startup job management
- **Aspire Integration**: .NET Aspire hosting

#### 3. **Coding Conventions**
- Naming conventions (projects, namespaces, entities, interfaces)
- File organization standards
- Code style guidelines
- Entity design patterns
- Repository pattern usage
- Configuration patterns

#### 4. **Advanced Features** (Comprehensive)

**Data Seeding**:
- Static model-based seeding (migrations)
- Dynamic runtime seeding (EF Core 9+)
- `DataSeedingConfiguration<T>` pattern
- Auto-discovery from assemblies

**Entity Lifecycle Hooks**:
- `IBeforeSaveHookAsync` / `IAfterSaveHookAsync`
- Audit hooks
- Event publishing hooks
- Validation hooks
- SnapshotContext integration

**Specification Pattern**:
- Reusable query logic
- Composable specifications
- Filtering, sorting, includes
- LinqKit integration

**Global Query Filters**:
- `IGlobalQueryFilter` interface
- Soft delete filters
- Multi-tenancy filters
- Auto-application

**DbContext Utilities**:
- Table name resolution
- Primary key utilities
- SQL Server sequences
- Navigation property helpers

**Snapshot Context**:
- Entity change tracking
- Before/after value comparison
- Property-level detection

**SlimBus CQRS**:
- Message contracts (IWithResponse, INoResponse, IWithPageResponse)
- Handler patterns
- FluentResults integration
- Minimal API mapping
- ProblemDetails responses
- Validation integration

#### 5. **Repository Pattern** (Detailed)

Complete interface documentation with examples:
- `IReadRepository<T>` - All 8 methods documented
- `IWriteRepository<T>` - All 10 methods documented
- `IRepository<T>` - Combined interface
- Usage examples for queries, commands, transactions
- Best practices

#### 6. **Testing Conventions**
- Test project naming
- Test class naming
- Test method naming (Method_Scenario_Expected)
- AAA pattern
- Tools: xUnit, FluentAssertions, AutoBogus

#### 7. **Common Patterns & Anti-Patterns**

**✅ DO Examples** (20+ patterns):
- Entity creation with validation
- Repository usage for queries vs commands
- CQRS message definitions
- Data seeding implementations
- Proper async/await
- Transaction management

**❌ DON'T Examples** (25+ anti-patterns):
- Public setters on entities
- Anemic domain models
- Missing EF Core constructors
- Writable collections
- Repository misuse
- N+1 queries
- Synchronous database calls
- Configuration repetition
- CQRS violations
- Testing implementation details

#### 8. **Quick Reference Materials**
- Global usings
- Common attributes
- Common interfaces
- Decision matrix (25+ scenarios)
- Integration checklist (18 steps)

## 🚀 How to Use

### For Developers

1. **Read once** to understand the framework
2. **Reference when needed** for specific patterns
3. **Share with new team members** for onboarding
4. **Update when patterns evolve**

### For GitHub Copilot

Copilot automatically reads files in `.github/` directory, especially:
- Files starting with `copilot-`
- Markdown files with framework documentation
- Architecture decision records

**Pro Tip**: When asking Copilot for help, reference specific sections:
```
"Using the patterns from the memory bank, create a new AuditedEntity for Customer"
"Following the repository pattern from the memory bank, implement a product query service"
"Based on the CQRS examples in the memory bank, create handlers for order management"
```

## 📊 Coverage Statistics

- **Total Lines**: 1000+
- **Code Examples**: 100+
- **Main Sections**: 15
- **Subsections**: 50+
- **Packages Documented**: 20+
- **Pattern Examples**: 45+
- **Anti-Pattern Warnings**: 25+

## 🔄 Maintenance

### When to Update

Update the memory bank when:
- ✅ New packages are added
- ✅ Architecture patterns change
- ✅ New conventions are established
- ✅ Common anti-patterns are discovered
- ✅ Best practices evolve
- ✅ New features are added to existing packages

### How to Update

1. Open `/src/.github/copilot-memory-bank.md`
2. Locate the relevant section
3. Add/modify content following the existing format
4. Update the "Last Updated" date
5. Commit with message: `docs: update copilot memory bank - [what changed]`

## 🎓 Benefits

### For the Team

1. **Faster Onboarding**: New developers understand patterns quickly
2. **Consistent Code**: Everyone follows the same conventions
3. **Better AI Suggestions**: Copilot generates code matching your style
4. **Documentation**: Centralized knowledge base
5. **Quality Assurance**: Anti-patterns are documented and avoided

### For AI/Copilot

1. **Context-Aware**: Understands project-specific patterns
2. **Accurate Suggestions**: Generates code that works with your framework
3. **Convention Following**: Matches naming, style, and structure
4. **Anti-Pattern Avoidance**: Knows what NOT to suggest
5. **Comprehensive Examples**: Has real working code to reference

## 📖 Sections Quick Reference

| Section | What It Covers | Lines |
|---------|---------------|-------|
| Project Overview | Architecture, principles, goals | ~50 |
| Project Structure | All packages, features, setup | ~200 |
| Coding Conventions | Naming, style, organization | ~150 |
| Advanced Features | Seeding, hooks, specs, filters | ~350 |
| Repository Pattern | Interfaces, examples, best practices | ~200 |
| Testing | Conventions, patterns, tools | ~80 |
| Patterns & Anti-Patterns | DO/DON'T examples | ~300 |
| Quick Reference | Checklists, matrices, summaries | ~100 |

## 🔗 Related Documentation

- **Main README**: `/README.md` - High-level project overview
- **Individual Package READMEs**: Each package has its own detailed README
- **Project Brief**: `/projectBrief.md` - Project analysis and improvements
- **Structure Checklist**: `/PROJECT_STRUCTURE_CHECKLIST.md` - Organizational standards

## ⚡ Quick Start for Copilot Users

Ask Copilot:
- "Show me how to create a new entity following DKNet patterns"
- "Create a repository for Product entity"
- "Generate a CQRS query handler for getting orders"
- "What's the correct way to seed data in DKNet?"
- "Show me the anti-patterns I should avoid"

Copilot will reference the memory bank to provide accurate, framework-specific suggestions!

## 🤝 Contributing

To improve the memory bank:
1. Identify missing patterns or unclear sections
2. Create an issue or PR with suggested improvements
3. Follow the existing format and style
4. Add code examples where helpful
5. Update the table of contents if adding new sections

## 📝 Version History

- **v1.0.0** (Oct 27, 2025) - Initial comprehensive memory bank
  - Full project structure documentation
  - All major patterns documented
  - 100+ code examples
  - 25+ anti-patterns identified
  - Complete CQRS and Repository patterns

---

**Maintained by**: Steven Hoang / DKNet Team  
**Last Updated**: October 27, 2025  
**Status**: ✅ Active and Maintained

For questions or suggestions, please open an issue on GitHub.

