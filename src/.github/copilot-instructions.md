# GitHub Copilot Agent Configuration for DKNet Framework

## Agent Identity
- **Project**: DKNet Framework
- **Type**: .NET 9 Library Collection
- **Focus**: EF Core Extensions, Specifications, Dynamic Predicates
- **Standards**: Enterprise-grade, production-ready code

## Core Instructions for AI Agent

### 1. ALWAYS Load Context First
Before generating ANY code, you MUST:
```
1. Read /memory-bank/README.md (navigation)
2. Read /memory-bank/activeContext.md (current focus)
3. Read /memory-bank/copilot-quick-reference.md (patterns)
4. Read /memory-bank/systemPatterns.md (detailed patterns)
5. Read /memory-bank/copilot-rules.md (complete standards)
```

**Priority**: README → activeContext → quick-reference → systemPatterns → copilot-rules

### 2. Project-Specific Knowledge

#### Current Development Focus
- **Active Area**: EfCore.Specifications - Dynamic Predicate System
- **Key Features**: 
  - Dynamic predicate building with `DynamicPredicateBuilder<T>`
  - Specification Pattern implementation
  - LinqKit integration for expression composition
  - TestContainers for integration testing
  - Enum validation and type safety

#### Technology Stack
- **.NET**: 9.0 with C# 13
- **EF Core**: 9.0
- **Testing**: xUnit, Shouldly, TestContainers.MsSql, Bogus
- **Dynamic LINQ**: System.Linq.Dynamic.Core, LinqKit

#### Code Quality Requirements
- `TreatWarningsAsErrors=true` - ZERO warnings allowed
- `<Nullable>enable</Nullable>` - Nullable reference types mandatory
- XML documentation required for all public APIs
- Test coverage: 85%+ target

### 3. Pattern Recognition Rules

When you see code involving:

#### Dynamic Predicates
→ Use: `PredicateBuilder.New<T>()`, `.DynamicAnd()`, `.DynamicOr()`
→ Always: Include `.AsExpandable()` before `.Where()`
→ Pattern: Null-safe dynamic expression building
```csharp
var predicate = PredicateBuilder.New<Product>()
    .And(p => p.IsActive)
    .DynamicAnd(builder => builder
        .With("PropertyName", FilterOperations.Operator, value));
```

#### EF Core Queries
→ Use: `.AsNoTracking()` for read-only
→ Use: `async`/`await` for all database operations
→ Filter: Push to database with `.Where()` before `.ToListAsync()`
→ Avoid: N+1 queries (use `.Include()` or projections)

#### Tests
→ Framework: xUnit with Shouldly assertions
→ Integration: TestContainers.MsSql (real SQL Server)
→ Naming: `MethodName_Scenario_ExpectedBehavior`
→ Structure: Arrange-Act-Assert

#### Extension Methods
→ Location: Static classes in `/Extensions` folder
→ Documentation: XML docs with `<summary>`, `<param>`, `<returns>`
→ Naming: Verb-based (e.g., `TryConvertToEnum`, `DynamicAnd`)

### 4. Code Generation Rules

#### When Generating Classes
```csharp
// <copyright file="ClassName.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

/// <summary>
///     Brief description of the class purpose.
/// </summary>
public class ClassName
{
    private readonly IService _service;
    
    /// <summary>
    ///     Description of what this constructor does.
    /// </summary>
    public ClassName(IService service)
    {
        _service = service;
    }
}
```

#### When Generating Methods
```csharp
/// <summary>
///     Description of what the method does.
/// </summary>
/// <typeparam name="T">Description of generic parameter</typeparam>
/// <param name="paramName">Description of parameter</param>
/// <returns>Description of return value</returns>
/// <exception cref="ArgumentNullException">When paramName is null</exception>
public async Task<Result<T>> MethodNameAsync<T>(string paramName)
{
    // Implementation with proper error handling
}
```

#### When Generating Tests
```csharp
[Fact]
public void MethodName_WhenScenarioOccurs_ThenExpectedOutcome()
{
    // Arrange: Setup test data and dependencies
    var testData = CreateTestData();
    
    // Act: Execute the operation under test
    var result = _sut.MethodUnderTest(testData);
    
    // Assert: Verify expected outcomes
    result.ShouldNotBeNull();
    result.ShouldBe(expectedValue);
}
```

### 5. Anti-Patterns to NEVER Generate

❌ **NEVER** use InMemory database for EF Core tests (use TestContainers)
❌ **NEVER** mix sync and async code (`result = asyncMethod().Result`)
❌ **NEVER** materialize queries early (`.ToList()` then `.Where()`)
❌ **NEVER** forget `.AsExpandable()` with LinqKit predicates
❌ **NEVER** omit XML documentation on public APIs
❌ **NEVER** include secrets or credentials in code
❌ **NEVER** use `async void` (except event handlers)
❌ **NEVER** catch exceptions without logging
❌ **NEVER** ignore nullable warnings
❌ **NEVER** violate the Single Responsibility Principle

### 6. Decision Making Guidelines

#### When Asked to Implement a Feature
1. **Check** `activeContext.md` - Is this aligned with current focus?
2. **Review** `systemPatterns.md` - What pattern should be used?
3. **Reference** `copilot-quick-reference.md` - Are there templates?
4. **Generate** code following established patterns
5. **Include** comprehensive tests (arrange-act-assert)
6. **Document** with XML comments
7. **Verify** zero warnings after build

#### When Asked to Fix a Bug
1. **Understand** the issue (read error messages, stack traces)
2. **Check** existing tests - Do they cover this scenario?
3. **Add** test that reproduces the bug (TDD approach)
4. **Fix** the code to make test pass
5. **Verify** all tests still pass
6. **Document** the fix in comments if non-obvious

#### When Asked to Refactor
1. **Ensure** tests exist and pass before refactoring
2. **Make** small, incremental changes
3. **Run** tests after each change
4. **Keep** functionality identical (tests prove this)
5. **Improve** code quality, readability, performance
6. **Update** documentation if patterns change

### 7. Quality Checklist

Before considering code complete, verify:
- [ ] Compiles without warnings
- [ ] All tests pass
- [ ] XML documentation on public APIs
- [ ] File header present
- [ ] Follows naming conventions
- [ ] Null safety considered
- [ ] Error handling implemented
- [ ] Performance considered (async, filtering)
- [ ] Security considered (no secrets, validation)
- [ ] Patterns followed (Specification, Repository, etc.)

### 8. Communication Guidelines

#### When Explaining Code
- Reference the specific pattern from `systemPatterns.md`
- Include code examples from actual codebase
- Explain WHY, not just WHAT
- Link to relevant documentation

#### When Suggesting Improvements
- Reference quality standards from `copilot-rules.md`
- Show before/after comparisons
- Explain benefits (performance, maintainability, etc.)
- Consider impact on existing code

#### When Reporting Issues
- Provide error messages and stack traces
- Show relevant code context
- Suggest potential solutions
- Reference similar solved issues if available

### 9. Special DKNet Framework Features

#### Dynamic Predicate Builder
- **Purpose**: Build EF Core queries from runtime conditions
- **Usage**: `.DynamicAnd(builder => builder.With(...))`
- **Key**: Type-safe, null-safe, composable
- **Gotcha**: Always use `.AsExpandable()` with LinqKit

#### Specification Pattern
- **Purpose**: Encapsulate query logic in reusable specifications
- **Base Class**: `Specification<TEntity>`
- **Features**: Criteria, Includes, OrderBy
- **Usage**: Compose with `.And()`, `.Or()` using LinqKit

#### Type Extensions
- **Purpose**: Type checking and conversion utilities
- **Key Method**: `TryConvertToEnum<TEnum>` with validation
- **Usage**: Safe enum conversion with culture-invariant parsing
- **Pattern**: Return `bool`, `out` parameter for result

#### TestContainers Integration
- **Purpose**: Real SQL Server for integration tests
- **Setup**: `IAsyncLifetime` fixture pattern
- **Benefits**: Catches SQL-specific issues, accurate testing
- **Usage**: One container per test class, dispose properly

### 10. Context-Aware Responses

When the user asks about:

**"How do I..."**
→ Check `copilot-quick-reference.md` for templates
→ Provide code example following DKNet patterns
→ Include test example
→ Reference full documentation

**"Why does..."**
→ Explain based on patterns in `systemPatterns.md`
→ Reference technical constraints in `techContext.md`
→ Show alternatives if applicable

**"Is this correct..."**
→ Compare against `copilot-rules.md` standards
→ Check pattern usage in `systemPatterns.md`
→ Suggest improvements if needed
→ Explain reasoning

**"What should I do next..."**
→ Check `activeContext.md` for current priorities
→ Reference `progress-detailed.md` for roadmap
→ Suggest aligned with project goals

---

## Quick Reference for Agent

### File Priority (Load Order)
1. `/memory-bank/README.md` - Navigation
2. `/memory-bank/activeContext.md` - Current work
3. `/memory-bank/copilot-quick-reference.md` - Quick patterns
4. `/memory-bank/systemPatterns.md` - Detailed patterns
5. `/memory-bank/copilot-rules.md` - Complete standards

### Critical Patterns
- **Specification Pattern**: Reusable query specifications
- **Dynamic Predicates**: Runtime query building
- **Repository Pattern**: Data access abstraction
- **TestContainers**: Real database testing
- **Arrange-Act-Assert**: Test structure

### Non-Negotiable Standards
- Zero warnings (`TreatWarningsAsErrors=true`)
- Nullable types enabled
- XML docs on public APIs
- Async/await for I/O
- TestContainers for integration tests

### Common Tasks
- Adding filter operation → Update `DynamicPredicateBuilder`, add tests
- Creating specification → Inherit from `Specification<T>`, implement `Criteria`
- Writing test → xUnit, Shouldly, `MethodName_Scenario_Outcome` naming
- Adding extension → Static class, XML docs, comprehensive tests

---

**Agent Version**: 1.0  
**Last Updated**: November 5, 2025  
**Status**: Production Ready

**Remember**: This configuration ensures consistent, high-quality code generation aligned with DKNet Framework standards and patterns.

