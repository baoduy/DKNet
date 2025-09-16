# Changelog

All notable changes to DKNet.EfCore.Repos.Abstractions will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Comprehensive README documentation with usage examples
- Standardized changelog following Keep a Changelog format
- Enhanced API documentation for all repository interfaces
- Advanced usage patterns including Unit of Work and Specifications
- CQRS integration examples and transaction management

### Changed
- N/A

### Deprecated
- N/A

### Removed
- N/A

### Fixed
- N/A

### Security
- N/A

## Migration Guide

### From Previous Versions

This section will be updated as breaking changes are introduced in future versions.

Currently, all APIs are stable and no migration is required.

#### Repository Interfaces
All repository interfaces maintain backward compatibility:
- `IRepository<TEntity>`
- `IReadRepository<TEntity>`
- `IWriteRepository<TEntity>`

#### Method Signatures
All method signatures remain stable:
- Read operations (Gets, FindAsync, GetDto, etc.)
- Write operations (AddAsync, UpdateAsync, DeleteAsync, etc.)
- Transaction operations (BeginTransactionAsync, SaveChangesAsync)

#### Generic Constraints
All generic type constraints maintain backward compatibility for entity types.

---

**Note**: This changelog follows the [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format.
For breaking changes, migration examples will be provided in this section.