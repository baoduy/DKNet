# Changelog

All notable changes to DKNet.EfCore.Hooks will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Comprehensive README documentation with usage examples
- Standardized changelog following Keep a Changelog format
- Enhanced API documentation for all hook interfaces
- Advanced usage patterns including performance monitoring, security, and external integrations
- Error handling and resilience patterns for production use

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

#### Hook Interfaces
All hook interfaces maintain backward compatibility:
- `IHookBaseAsync`
- `IBeforeSaveHookAsync`
- `IAfterSaveHookAsync`
- `IHookAsync`

#### Service Registration
All service registration methods maintain backward compatibility:
- `AddHook<TDbContext, THook>()`
- `AddHookInterceptor<TDbContext>(IServiceProvider)`

#### SnapshotContext
All SnapshotContext features maintain backward compatibility:
- `SnapshotContext.Entries`
- `SnapshotEntityEntry` properties and methods

---

**Note**: This changelog follows the [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format.
For breaking changes, migration examples will be provided in this section.