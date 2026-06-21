---
title: Column Encryption
category: EF Core Persistence
tags: [efcore, encryption, hooks, security, data-protection]
---

## Summary

DKNet's EF Core column encryption transparently encrypts and decrypts string
properties as they cross the persistence boundary. Properties opt in with the
`[Encrypted]` marker attribute, and a SaveChanges interceptor handles the
cryptographic work — another cross-cutting concern built on [[savechanges-hooks]].

## Opting in

`[Encrypted]` is a marker attribute applied to string properties to enable automatic
column-level encryption. The application code continues to read and write plain string
values; the framework encrypts on write and decrypts on read so the ciphertext only
exists at rest.

## How it integrates with persistence

The encryption hook runs in the SaveChanges pipeline: in the before-write phase it
encrypts the marked properties of tracked entities, and the corresponding read path
decrypts them. Because it is an opt-in interceptor registered via DI, it composes with
the [[audit-logs]] and [[data-authorization]] concerns on the same `DbContext`.

## Relationship to other crypto features

Column encryption is about protecting persisted data at the EF Core boundary. For
general-purpose application cryptography (AES, AES-GCM, RSA, SHA, HMAC) used outside of
persistence, see the standalone [[encryption-services]] package. The `[Encrypted]`
attribute itself is part of the domain contracts in [[efcore-abstractions]].
