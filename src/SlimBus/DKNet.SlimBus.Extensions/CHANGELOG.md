# Changelog

All notable changes to DKNet.SlimBus.Extensions will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Comprehensive README documentation with usage examples
- Standardized changelog following Keep a Changelog format
- Enhanced API documentation for all fluent interfaces

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

#### Fluent Interfaces
All fluent interfaces maintain backward compatibility:
- `Fluents.Requests.*`
- `Fluents.Queries.*`
- `Fluents.EventsConsumers.*`

#### EF Core Integration
All EF Core integration methods maintain backward compatibility:
- `AddSlimBusForEfCore()`
- `EfAutoSavePostProcessor<,>`

#### Result Patterns
All result pattern interfaces maintain backward compatibility with FluentResults.

---

**Note**: This changelog follows the [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format.
For breaking changes, migration examples will be provided in this section.