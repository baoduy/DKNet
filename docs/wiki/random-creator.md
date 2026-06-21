---
title: DKNet.RandomCreator
category: Core Utilities
tags: [core, random, security, strings]
---

## Summary

`DKNet.RandomCreator` is a small Core Foundation package that provides secure random
string and character generation. Its public facade, `RandomCreators`, exposes
`NewChars` and `NewString` to produce random char arrays or strings, wrapping a
disposable `StringCreator` configured by options.

## API surface

- **`RandomCreators.NewChars(...)`** — generates a random character array.
- **`RandomCreators.NewString(...)`** — generates a random string.

Both delegate to an internal `StringCreator` (with a `StringCreatorOptions`-style
configuration) that encapsulates the generation logic and is disposed after use.

## Use cases

The package is useful anywhere a framework or application needs unpredictable values —
tokens, identifiers, temporary secrets, or test fixtures. Being framework-agnostic and
dependency-light, it sits alongside [[fw-extensions]] in the innermost ring of
[[onion-architecture]].

## Relation to other features

For application-grade cryptography (AES, RSA, hashing, HMAC) rather than random value
generation, use the dedicated [[encryption-services]] package. For column-level data
protection in EF Core, see [[column-encryption]]. `RandomCreator` is intentionally
narrow: it just creates random characters and strings.
