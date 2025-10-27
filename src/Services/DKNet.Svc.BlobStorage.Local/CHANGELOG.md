# Changelog

All notable changes to DKNet.Svc.BlobStorage.Local will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Comprehensive README documentation with usage examples
- Standardized changelog following Keep a Changelog format
- Enhanced API documentation for local file system integration
- Advanced usage patterns including file organization, archiving, synchronization, and file watching
- Cross-platform considerations and security guidelines
- Performance optimization recommendations

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

All local file system service implementations maintain backward compatibility:

- `LocalBlobService` implementing `IBlobService`
- All method signatures remain stable

#### Configuration

All configuration options maintain backward compatibility:

- `LocalDirectoryOptions`
- `AddLocalDirectoryBlobService()` registration method

#### File System Operations

All file system operations maintain backward compatibility:

- Path handling and directory creation
- Metadata storage mechanisms
- Cross-platform file operations

---

**Note**: This changelog follows the [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format.
For breaking changes, migration examples will be provided in this section.