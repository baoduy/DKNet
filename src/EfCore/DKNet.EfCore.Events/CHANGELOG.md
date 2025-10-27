# Changelog

All notable changes to DKNet.EfCore.Events will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Comprehensive README documentation with usage examples
- Standardized changelog following Keep a Changelog format
- Enhanced API documentation for all public interfaces and classes
- Advanced error handling patterns and retry mechanisms
- Complex domain event scenarios with aggregate roots

### Changed

- Enhanced README with detailed event lifecycle and best practices
- Improved code examples with real-world scenarios

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

#### Event Publisher

All event publisher interfaces maintain backward compatibility:

- `IEventPublisher`
- `AddEventPublisher<TDbContext, TImplementation>()`

#### Entity Events

All entity event interfaces maintain backward compatibility:

- `IEventEntity` (from DKNet.EfCore.Abstractions)
- `EntityEventItem`
- Event queuing and clearing methods

#### Event Handling

All event handling patterns maintain backward compatibility:

- Domain event patterns
- Event handler registration
- Automatic event publishing during SaveChanges

---

**Note**: This changelog follows the [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format.
For breaking changes, migration examples will be provided in this section.