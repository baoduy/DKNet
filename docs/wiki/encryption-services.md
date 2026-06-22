---
title: Encryption Services
category: Service Adapters
tags: [encryption, aes, rsa, hashing, hmac, security]
---

## Summary

`DKNet.Svc.Encryption` is a standalone cryptographic service package offering symmetric
and asymmetric encryption plus hashing primitives. A single DI extension,
`AddEncryptionServices`, registers all of the interfaces — AES, AES-GCM, RSA, SHA, and
HMAC — as transient implementations, making application-level cryptography a
straightforward injected dependency.

## Provided primitives

- **AES and AES-GCM** — symmetric encryption, with AES-GCM supporting associated data
  and authenticated tamper detection.
- **RSA** — asymmetric encryption for end-to-end scenarios.
- **SHA (256/512)** — cryptographic hashing.
- **HMAC (256/512)** — keyed message authentication.

`EncryptionSetup.AddEncryptionServices` wires all of these up at once; tests cover
AES-GCM associated-data tampering, HMAC, SHA hashing, RSA round-trips, and
post-dispose guards.

## Distinction from EF Core column encryption

This package is for general application cryptography invoked explicitly in code. It is
distinct from [[column-encryption]], which transparently protects `[Encrypted]` string
properties at the EF Core persistence boundary. For generating unpredictable values
rather than encrypting, see [[random-creator]].

## Where it fits

Encryption services are one of the pluggable service adapters alongside
[[blob-storage]], [[pdf-generators]], and [[transformation]], sitting in the Service
Adapters ring of [[onion-architecture]].
