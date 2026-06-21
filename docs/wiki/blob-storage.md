---
title: Blob Storage
category: Service Adapters
tags: [storage, blob, azure, aws-s3, local, adapter]
---

## Summary

DKNet's blob storage is a provider-agnostic abstraction: application code depends on
the `IBlobService` contract, and a concrete adapter — Azure Storage, AWS S3, or the
local filesystem — provides the implementation. Swapping providers is a configuration
and DI change rather than a code change, the hallmark of a service adapter in
[[onion-architecture]].

## The abstraction and shared options

The abstractions package defines `IBlobService` and a base `BlobServiceOptions` record
holding validation rules — allowed extensions, maximum name length, and maximum file
size — that every provider inherits. Concrete option types (S3, Azure, Local) extend
this base, so validation behaves consistently regardless of backend.

## Available adapters

- **Azure Storage** (`DKNet.Svc.BlobStorage.AzureStorage`) — backed by
  `Azure.Storage.Blobs`.
- **AWS S3** (`DKNet.Svc.BlobStorage.AwsS3`) — backed by the AWS SDK; supports
  pre-signed URLs, multipart uploads, and S3-compatible services such as Cloudflare R2
  and MinIO.
- **Local** (`DKNet.Svc.BlobStorage.Local`) — `LocalBlobService` stores blobs under a
  configured root folder with path-safety checks and listing support.

## Where it fits

Blob storage is one of the pluggable service adapters, alongside [[pdf-generators]],
[[encryption-services]], and [[transformation]]. Because consumers depend only on
`IBlobService`, the choice of provider stays out of the domain and application layers,
and validation rules are centralized rather than duplicated per provider.
