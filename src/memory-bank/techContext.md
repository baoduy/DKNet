# Tech Context

## Technology Stack

### Core Framework
- **.NET 9.0**: Latest LTS version with C# 13 language features
- **C# Language Version**: Latest (13.0)
- **Nullable Reference Types**: Enabled globally (`<Nullable>enable</Nullable>`)
- **LangVersion**: `latest` for all projects

### Key Dependencies

#### EF Core Stack
- **Microsoft.EntityFrameworkCore**: 9.0.0
- **Microsoft.EntityFrameworkCore.SqlServer**: SQL Server provider
- **Microsoft.EntityFrameworkCore.Relational**: Base for relational providers
- **LinqKit.Microsoft.EntityFrameworkCore**: Expression composition and dynamic queries

#### Dynamic LINQ & Specifications
- **System.Linq.Dynamic.Core**: Dynamic LINQ parsing for string-based queries
- **LinqKit**: Expression tree manipulation and composition
- Custom `DynamicPredicateBuilder<TEntity>` for type-safe dynamic filtering

#### Testing Framework
- **xUnit**: Primary test framework
- **Shouldly**: Fluent assertion library
- **Testcontainers**: Docker-based integration testing
- **Testcontainers.MsSql**: SQL Server container for EF Core integration tests
- **Bogus**: Fake data generation for tests
- **Microsoft.EntityFrameworkCore.InMemory**: In-memory database for unit tests (used selectively)

#### Background Tasks
- **Microsoft.Extensions.Hosting**: IHostedService and background service abstractions
- **Microsoft.Extensions.DependencyInjection**: DI container integration

#### Messaging
- **Azure.Messaging.ServiceBus**: Azure Service Bus SDK
- **.NET Aspire**: Container orchestration and local development

### Build & Code Quality

#### Analyzers & Code Style
```xml
<PropertyGroup>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

- **Microsoft.CodeAnalysis.NetAnalyzers**: Enabled with `TreatWarningsAsErrors`
- **StyleCop.Analyzers**: Code style enforcement (configured in `stylecop.json`)
- **ReSharper**: Team-wide settings in `.DotSettings` file

#### Package Management
- **Central Package Management**: All versions in `Directory.Packages.props`
- **Directory.Build.props**: Shared build properties and analyzer references
- **NuGet.config**: Custom package sources if needed

### Development Tools & Constraints

#### IDE Support
- **JetBrains Rider**: Primary IDE (ReSharper settings included)
- **Visual Studio 2022**: Full support
- **Visual Studio Code**: With C# extension

#### Version Control
- **Git**: Primary VCS
- **GitHub**: Repository hosting
- **Semantic Versioning**: For NuGet packages (9.9.YYMMDD format)

#### CI/CD Considerations
- Code must compile with `TreatWarningsAsErrors=true`
- XML documentation required for all public APIs
- All tests must pass before merge
- Code coverage reports generated with `coverage.runsettings`

### Architecture Constraints

#### Design Patterns
- **Repository Pattern**: Generic repositories with specification support
- **Specification Pattern**: For complex query composition
- **Unit of Work**: Transaction management through DbContext
- **Factory Pattern**: Service creation and dependency injection
- **Builder Pattern**: Fluent API for dynamic predicate construction

#### Performance Guidelines
- Always use `IQueryable<T>` for EF Core queries (avoid `IEnumerable<T>` prematurely)
- Use `.AsNoTracking()` for read-only queries
- Avoid N+1 queries (use `.Include()` or projections)
- Use async/await for all I/O operations
- Enable query splitting for large includes when appropriate

#### Testing Strategy
- **Unit Tests**: Fast, isolated tests using in-memory databases or mocks
- **Integration Tests**: TestContainers with real SQL Server for EF Core tests
- **Test Isolation**: Each test manages its own database state
- **Arrange-Act-Assert**: Standard test structure
- **Descriptive Test Names**: `MethodName_Scenario_ExpectedBehavior` convention

### Security Constraints
- No secrets in source code (use environment variables or user secrets)
- Sensitive data encryption using EF Core encryption extensions
- Row-level security through data authorization filters
- Audit logging for compliance requirements

### Platform Targets
- **Runtime**: .NET 9.0+
- **OS**: Cross-platform (Windows, Linux, macOS)
- **Databases**: SQL Server (primary), potentially other EF Core providers
- **Containers**: Docker support via TestContainers and Aspire
