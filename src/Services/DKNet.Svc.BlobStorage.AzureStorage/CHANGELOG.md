# Changelog

All notable changes to DKNet.Svc.BlobStorage.AzureStorage will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Comprehensive README documentation with usage examples
- Standardized changelog following Keep a Changelog format
- Enhanced API documentation for Azure Storage integration
- Advanced usage patterns including batch operations, lifecycle management, and ASP.NET Core integration
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

#### Service Implementation

All Azure Storage service implementations maintain backward compatibility:

- `AzureStorageBlobService` implementing `IBlobService`
- All method signatures remain stable

#### Configuration

All configuration options maintain backward compatibility:

- `AzureStorageOptions`
- `AddAzureStorageAdapter()` registration method

#### Connection Strings

All connection string formats remain supported:

- Storage account connection strings
- SAS token connection strings
- Development storage (Azurite) connection strings

---

**Note**: This changelog follows the [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format.
For breaking changes, migration examples will be provided in this section.