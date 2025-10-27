# Changelog

All notable changes to DKNet.EfCore.Repos will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Comprehensive README documentation with usage examples
- Standardized changelog following Keep a Changelog format
- Enhanced API documentation for all repository implementations
- Advanced usage patterns including caching, MediatR integration, and bulk operations
- Unit of Work pattern examples and CQRS implementation guides

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

#### Repository Implementations

All repository implementations maintain backward compatibility:

- `Repository<TEntity>`
- `ReadRepository<TEntity>`
- `WriteRepository<TEntity>`

#### Service Registration

All service registration extensions maintain backward compatibility:

- `AddGenericRepositories<TDbContext>()`

#### Mapster Integration

All Mapster integration features maintain backward compatibility:

- `GetDto<TModel>()` projections
- Automatic mapper injection

---

**Note**: This changelog follows the [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format.
For breaking changes, migration examples will be provided in this section.