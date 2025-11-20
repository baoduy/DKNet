# DKNet Framework Changelog

All notable changes to the DKNet Framework will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Consolidated repository-wide documentation in `docs/` folder
- Comprehensive getting started guide
- Configuration and setup documentation
- Examples and recipes section
- API reference documentation
- Migration guide for breaking changes
- FAQ and best practices section

### Changed
- Improved documentation organization and navigation
- Enhanced main README.md to be more concise and point to docs/

## [2024.12.0] - 2024-12-XX

### Added
- Complete framework redesign with Domain-Driven Design principles
- Onion Architecture implementation across all components
- SlimBus template for rapid API development with CQRS patterns
- Comprehensive Entity Framework Core extensions
- Repository pattern with specifications support
- Domain event handling and dispatching
- Data authorization and tenant-aware filtering
- Blob storage abstractions for Azure, AWS S3, and local storage
- Comprehensive testing strategy with 99% coverage goals
- .NET 10.0 support across all packages with C# 14 language features

### Core Framework (DKNet.Fw.Extensions)
- Added comprehensive extension methods for types, properties, and enums
- Async enumerable extensions with full support
- Property utilities for dynamic access and manipulation
- Type checking and conversion utilities
- Enhanced error handling patterns

### Entity Framework Core Extensions
- **DKNet.EfCore.Abstractions**: Core interfaces and base classes
- **DKNet.EfCore.Extensions**: EF Core functionality enhancements
- **DKNet.EfCore.Repos**: Repository pattern implementations
- **DKNet.EfCore.Repos.Abstractions**: Repository interface definitions
- **DKNet.EfCore.Hooks**: Entity lifecycle hooks and interceptors
- **DKNet.EfCore.Events**: Domain event handling system
- **DKNet.EfCore.DataAuthorization**: Row-level security and filtering
- **DKNet.EfCore.Specifications**: Specification pattern for queries

### Messaging & CQRS
- **DKNet.SlimBus.Extensions**: SlimMessageBus integration for EF Core
- Command and query handler patterns
- Event-driven architecture support
- Message bus integration with domain events

### Service Layer
- **DKNet.Svc.BlobStorage.Abstractions**: File storage service interfaces
- **DKNet.Svc.BlobStorage.AzureStorage**: Azure Blob Storage implementation
- **DKNet.Svc.BlobStorage.AwsS3**: AWS S3 storage implementation
- **DKNet.Svc.BlobStorage.Local**: Local file system storage
- **DKNet.Svc.Transformation**: Data transformation utilities

### Templates
- **SlimBus.ApiEndpoints**: Complete API template with:
  - Minimal API endpoints with versioning
  - CQRS pattern implementation
  - Domain-driven design structure
  - Entity Framework Core integration
  - Authentication and authorization
  - Testing examples with TestContainers
  - Docker support and deployment configurations

### Infrastructure
- **.NET Aspire integration**: Service discovery and orchestration
- **Comprehensive CI/CD**: GitHub Actions workflows
- **Code quality**: SonarCloud, CodeQL, and Codecov integration
- **Package management**: Centralized version management
- **Documentation**: Auto-generated API docs and GitHub Pages

### Testing
- **99% code coverage** targets for core libraries
- **TestContainers** integration for reliable integration tests
- **Shouldly** assertions throughout test suite
- **Architecture tests** to enforce design constraints
- **Performance benchmarking** capabilities

## Package Version History

### Core Packages
- `DKNet.Fw.Extensions`: 1.0.0+ (Core framework extensions)
- `DKNet.RandomCreator`: 1.0.0+ (Random data generation utilities)

### Entity Framework Core Packages
- `DKNet.EfCore.Abstractions`: 1.0.0+
- `DKNet.EfCore.Extensions`: 1.0.0+
- `DKNet.EfCore.Repos`: 1.0.0+
- `DKNet.EfCore.Repos.Abstractions`: 1.0.0+
- `DKNet.EfCore.Hooks`: 1.0.0+
- `DKNet.EfCore.Events`: 1.0.0+
- `DKNet.EfCore.DataAuthorization`: 1.0.0+
- `DKNet.EfCore.Specifications`: 1.0.0+
- `DKNet.EfCore.Relational.Helpers`: 1.0.0+

### Messaging Packages
- `DKNet.SlimBus.Extensions`: 1.0.0+

### Service Packages
- `DKNet.Svc.BlobStorage.Abstractions`: 1.0.0+
- `DKNet.Svc.BlobStorage.AzureStorage`: 1.0.0+
- `DKNet.Svc.BlobStorage.AwsS3`: 1.0.0+
- `DKNet.Svc.BlobStorage.Local`: 1.0.0+
- `DKNet.Svc.Transformation`: 1.0.0+

### Aspire Packages
- `Aspire.Hosting.ServiceBus`: 1.0.0+

## Breaking Changes

### From Legacy to 2024.12.0
This represents a complete rewrite of the framework with focus on:

1. **Architecture**: Migration to Domain-Driven Design and Onion Architecture
2. **Technology**: Upgrade to .NET 10.0 with C# 14 language features
3. **Patterns**: Introduction of CQRS, Event Sourcing, and Repository patterns
4. **Testing**: Comprehensive test coverage with modern testing approaches
5. **Documentation**: Complete documentation overhaul with practical examples

### Migration Path
For existing users of legacy DKNet packages:

1. **Review New Architecture**: Understand DDD and Onion Architecture principles
2. **Use Templates**: Start with SlimBus template for new projects
3. **Gradual Migration**: Replace components incrementally
4. **Follow Examples**: Use provided examples and recipes for implementation
5. **Testing Strategy**: Adopt new testing patterns with TestContainers

## Security Updates

All packages include security enhancements:
- Secure defaults for all configurations
- Input validation and sanitization
- Protection against common vulnerabilities
- Regular dependency updates
- Security scanning in CI/CD pipeline

## Performance Improvements

- Optimized Entity Framework Core queries
- Efficient domain event dispatching
- Minimal allocations in hot paths
- Async/await patterns throughout
- Lazy loading and caching strategies

---

## Individual Package Changelogs

For detailed package-specific changes, see:

### Core
- [DKNet.Fw.Extensions Changelog](../src/Core/DKNet.Fw.Extensions/CHANGELOG.md)

### Entity Framework Core
- [DKNet.EfCore.Abstractions Changelog](../src/EfCore/DKNet.EfCore.Abstractions/CHANGELOG.md)
- [DKNet.EfCore.Extensions Changelog](../src/EfCore/DKNet.EfCore.Extensions/CHANGELOG.md)
- [DKNet.EfCore.Repos Changelog](../src/EfCore/DKNet.EfCore.Repos/CHANGELOG.md)
- [DKNet.EfCore.Hooks Changelog](../src/EfCore/DKNet.EfCore.Hooks/CHANGELOG.md)

### Messaging
- [DKNet.SlimBus.Extensions Changelog](../src/SlimBus/DKNet.SlimBus.Extensions/CHANGELOG.md)

### Services
- [DKNet.Svc.BlobStorage.Abstractions Changelog](../src/Services/DKNet.Svc.BlobStorage.Abstractions/CHANGELOG.md)
- [DKNet.Svc.BlobStorage.AzureStorage Changelog](../src/Services/DKNet.Svc.BlobStorage.AzureStorage/CHANGELOG.md)
- [DKNet.Svc.BlobStorage.AwsS3 Changelog](../src/Services/DKNet.Svc.BlobStorage.AwsS3/CHANGELOG.md)
- [DKNet.Svc.BlobStorage.Local Changelog](../src/Services/DKNet.Svc.BlobStorage.Local/CHANGELOG.md)

---

> 📝 **Note**: This consolidated changelog provides an overview of the entire framework. For detailed, package-specific changes, please refer to the individual package changelogs linked above.