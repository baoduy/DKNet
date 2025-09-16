# Changelog

All notable changes to DKNet.EfCore.Extensions will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Comprehensive README documentation with usage examples
- Standardized changelog following Keep a Changelog format
- Enhanced API documentation for all public interfaces and classes

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

#### Auto Configuration
All auto-configuration methods maintain backward compatibility:
- `UseAutoConfigModel<TContext>()`
- `AddGlobalModelBuilderRegister<T>()`

#### Extensions
All extension methods maintain backward compatibility:
- `GetTableName(Type)`
- `GetPrimaryKeyProperty(Type)`
- `GetPrimaryKeyValue(object)`
- `SeedDataAsync<TEntity>()`

#### Configuration Classes
All configuration base classes maintain backward compatibility:
- `DefaultEntityTypeConfiguration<TEntity>`
- `IGlobalQueryFilterRegister`
- `IDataSeedingConfiguration<TEntity>`

---

**Note**: This changelog follows the [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format.
For breaking changes, migration examples will be provided in this section.