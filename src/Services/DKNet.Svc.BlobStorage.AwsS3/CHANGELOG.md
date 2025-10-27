# Changelog

All notable changes to DKNet.Svc.BlobStorage.AwsS3 will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Comprehensive README documentation with usage examples
- Standardized changelog following Keep a Changelog format
- Enhanced API documentation for AWS S3 integration
- Advanced usage patterns including multipart uploads, lifecycle management, and CDN integration
- S3-compatible services support (Cloudflare R2, MinIO)
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

All AWS S3 service implementations maintain backward compatibility:

- `S3BlobService` implementing `IBlobService`
- All method signatures remain stable

#### Configuration

All configuration options maintain backward compatibility:

- `S3Options`
- `AddS3BlobService()` registration method

#### Authentication Methods

All authentication methods remain supported:

- Access key and secret configuration
- IAM role-based authentication
- Environment variable configuration

---

**Note**: This changelog follows the [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format.
For breaking changes, migration examples will be provided in this section.